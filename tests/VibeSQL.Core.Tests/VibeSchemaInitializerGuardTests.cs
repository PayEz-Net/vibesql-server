using System.Collections.Concurrent;
using System.Reflection;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Npgsql;
using Testcontainers.PostgreSql;
using VibeSQL.Core.Data;

namespace VibeSQL.Core.Tests;

/// <summary>
/// PAY-1865 (BAPert 65265 item 4, 65301(a)): the POPULATED-DB no-DDL guard, pinned by a committed,
/// re-runnable row rather than DotNetPert's original throwaway container.
///
/// WHY A REAL POSTGRES. The guard's whole claim is "on a populated database the startup path executes
/// ZERO DDL", and the only honest way to observe that is to watch a real server for DDL. An in-memory
/// fake would assert the code's own idea of DDL. So each test gets its own throwaway postgres:16
/// (Testcontainers.PostgreSql 4.1.0, MIT) and an EVENT TRIGGER on ddl_command_start that records every
/// DDL utility statement the initializer issues. ddl_command_start fires BEFORE the command runs, so it
/// catches even an idempotent no-op (CREATE TABLE IF NOT EXISTS on an existing table) - the exact case a
/// before/after catalog count would MISS.
///
/// ROWS (BAPert 65301a):
///   A. unset        -> skip, NO connection attempted, vibe schema absent.
///   B. set + empty  -> provisions; a second run is a no-op (marker present, counts unchanged).
///   C. set + populated (marker absent, vibe.documents has rows) -> ZERO DDL AND the marker is NOT written.
///
/// RED UNDER A MUTANT (proved, recorded in the commit): delete the IsPopulatedAsync gate in
/// VibeSchemaInitializer.ExecuteAsync and row C FAILS - the full schema is re-issued (ddl_log non-empty)
/// and the marker is written.
///
/// CI: tagged Integration and REQUIRES DOCKER. Where docker is absent, filter it out with
///   dotnet test --filter "Category!=Integration"
/// </summary>
[Trait("Category", "Integration")]
public class VibeSchemaInitializerGuardTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16")
        .Build();

    public Task InitializeAsync() => _container.StartAsync();
    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    // ── helpers ───────────────────────────────────────────────────────────────────────────────────

    private sealed record LogEntry(LogLevel Level, string Message);

    private sealed class CapturingLogger : ILogger<VibeSchemaInitializer>
    {
        public ConcurrentQueue<LogEntry> Entries { get; } = new();
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => Entries.Enqueue(new LogEntry(logLevel, formatter(state, exception)));
    }

    /// <summary>ExecuteAsync is protected on BackgroundService; invoking it directly is deterministic
    /// (no polling on a background task) and, for gate 1, proves the early return.</summary>
    private static async Task RunInitializerAsync(string? ownerConnectionString, CapturingLogger logger)
    {
        var initializer = new VibeSchemaInitializer(ownerConnectionString, logger);
        var method = typeof(VibeSchemaInitializer)
            .GetMethod("ExecuteAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        await (Task)method.Invoke(initializer, new object[] { CancellationToken.None })!;
    }

    private async Task<string> CreateDatabaseAsync(string dbName)
    {
        await using (var admin = new NpgsqlConnection(_container.GetConnectionString()))
        {
            await admin.OpenAsync();
            await using var create = new NpgsqlCommand($"CREATE DATABASE \"{dbName}\";", admin);
            await create.ExecuteNonQueryAsync();
        }

        var builder = new NpgsqlConnectionStringBuilder(_container.GetConnectionString()) { Database = dbName };
        return builder.ConnectionString;
    }

    private static async Task<T> ScalarAsync<T>(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        return (T)(await command.ExecuteScalarAsync())!;
    }

    private static async Task ExecAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static Task<bool> SchemaExistsAsync(string cs) => ScalarAsync<bool>(cs,
        "SELECT EXISTS (SELECT 1 FROM information_schema.schemata WHERE schema_name = 'vibe');");

    private static Task<long> VibeIndexCountAsync(string cs) => ScalarAsync<long>(cs,
        "SELECT count(*) FROM pg_indexes WHERE schemaname = 'vibe';");

    private static Task<bool> MarkerExistsAsync(string cs) => ScalarAsync<bool>(cs,
        "SELECT EXISTS (SELECT 1 FROM vibe.schema_meta WHERE key = 'initialized');");

    /// <summary>Install the DDL-observability objects. ddl_command_start fires before a utility command
    /// executes, so a skipped IF NOT EXISTS is still recorded.</summary>
    private static async Task InstallDdlCaptureAsync(string cs)
    {
        await ExecAsync(cs, """
            CREATE SCHEMA IF NOT EXISTS test_obs;
            CREATE TABLE IF NOT EXISTS test_obs.ddl_log (id SERIAL PRIMARY KEY, tag TEXT NOT NULL, at TIMESTAMPTZ DEFAULT NOW());
            CREATE OR REPLACE FUNCTION test_obs.log_ddl() RETURNS event_trigger LANGUAGE plpgsql AS $$
            BEGIN
                INSERT INTO test_obs.ddl_log(tag) VALUES (tg_tag);
            END $$;
            DROP EVENT TRIGGER IF EXISTS test_obs_ddl_start;
            CREATE EVENT TRIGGER test_obs_ddl_start ON ddl_command_start EXECUTE FUNCTION test_obs.log_ddl();
            """);
    }

    private static Task ResetDdlLogAsync(string cs) => ExecAsync(cs, "TRUNCATE test_obs.ddl_log;");

    private static Task<long> DdlCountAsync(string cs) => ScalarAsync<long>(cs, "SELECT count(*) FROM test_obs.ddl_log;");

    // ── A. unset -> skip, no connection ──────────────────────────────────────────────────────────

    [Fact]
    public async Task A_unset_owner_connection_skips_entirely_and_touches_no_database()
    {
        var cs = await CreateDatabaseAsync($"guard_unset_{Guid.NewGuid():N}");
        await InstallDdlCaptureAsync(cs);
        await ResetDdlLogAsync(cs);

        // Gate 1 is keyed on the string being NULL/WHITESPACE, and it returns BEFORE any
        // NpgsqlConnection is constructed - that early return is what "no connection is attempted"
        // means here. (A non-empty-but-broken string would take the CONNECT path by design; that is
        // not this gate.) The skip log is the observable proof; the schema proves no DDL ran.
        var loggerNull = new CapturingLogger();
        await RunInitializerAsync(null, loggerNull);

        loggerNull.Entries.Should().Contain(e => e.Message.Contains("schema provisioning skipped: no owner connection"),
            "gate 1 must log the skip at Information when the owner connection is null");
        (await SchemaExistsAsync(cs)).Should().BeFalse("no DDL may run when the owner connection is unset");

        var loggerWhite = new CapturingLogger();
        await RunInitializerAsync("   ", loggerWhite);
        loggerWhite.Entries.Should().Contain(e => e.Message.Contains("schema provisioning skipped: no owner connection"),
            "a whitespace-only connection string must be treated as unset");
        (await DdlCountAsync(cs)).Should().Be(0);
    }

    // ── B. set + empty -> provisions; second run is a no-op ──────────────────────────────────────

    [Fact]
    public async Task B_set_and_empty_provisions_and_a_second_run_is_a_noop()
    {
        var cs = await CreateDatabaseAsync($"guard_empty_{Guid.NewGuid():N}");

        var first = new CapturingLogger();
        await RunInitializerAsync(cs, first);

        (await SchemaExistsAsync(cs)).Should().BeTrue("an empty database is provisioned");
        (await MarkerExistsAsync(cs)).Should().BeTrue("the explicit marker is written after provisioning");
        var indexesAfterFirst = await VibeIndexCountAsync(cs);
        indexesAfterFirst.Should().BeGreaterThan(0, "the base schema creates indexes");

        var second = new CapturingLogger();
        await RunInitializerAsync(cs, second);

        second.Entries.Should().Contain(e => e.Message.Contains("already provisioned"),
            "gate 2 must short-circuit on the marker");
        (await VibeIndexCountAsync(cs)).Should().Be(indexesAfterFirst, "a second run must change nothing");
    }

    // ── C. set + populated -> ZERO DDL, no marker  (RED under the guard-removal mutant) ──────────

    [Fact]
    public async Task C_populated_database_runs_zero_ddl_and_writes_no_marker()
    {
        var cs = await CreateDatabaseAsync($"guard_populated_{Guid.NewGuid():N}");

        // Get a real schema in place, then make it look like a live database: remove the provisioning
        // marker and put a row in vibe.documents. This is the state prod/93 is in.
        await RunInitializerAsync(cs, new CapturingLogger());
        await ExecAsync(cs, "DELETE FROM vibe.schema_meta WHERE key = 'initialized';");
        await ExecAsync(cs, """
            INSERT INTO vibe.documents (client_id, collection, table_name, data)
            VALUES (1, 'agent_mail', 'agent_mail_inbox', '{"id":1}'::jsonb);
            """);

        await InstallDdlCaptureAsync(cs);
        await ResetDdlLogAsync(cs);

        var indexesBefore = await VibeIndexCountAsync(cs);
        var logger = new CapturingLogger();
        await RunInitializerAsync(cs, logger);

        logger.Entries.Should().Contain(e => e.Message.Contains("database is POPULATED"),
            "gate 3 must log the populated refusal");
        (await DdlCountAsync(cs)).Should().Be(0,
            "BAPert's hard rule: on a POPULATED database the startup path executes ZERO DDL - a plain " +
            "CREATE at boot would lock the partitioned vibe.documents");
        (await MarkerExistsAsync(cs)).Should().BeFalse(
            "the marker must NOT be written when the guard refuses to provision");
        (await VibeIndexCountAsync(cs)).Should().Be(indexesBefore, "no index may be created or dropped");
        (await ScalarAsync<long>(cs,
            "SELECT count(*) FROM vibe.documents WHERE collection = 'agent_mail';"))
            .Should().Be(1, "the existing row must be untouched");
    }
}
