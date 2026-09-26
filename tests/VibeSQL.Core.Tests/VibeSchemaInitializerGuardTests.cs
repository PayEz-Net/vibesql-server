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
/// ROWS:
///   A.  unset        -> skip, NO connection attempted, vibe schema absent.
///   B.  set + fresh (no vibe.documents) -> provisions; a second run is a no-op (marker, counts unchanged).
///   C.  vibe.documents present WITHOUT our marker -> NOT OURS: ZERO DDL, no marker written.
///   C2. MUST-1 (NightHawk 65322): the SAME as C but with a NON-SUPERUSER owner under FORCE RLS - the
///       realistic install. The old row-count gate read a populated database as empty here; C2 is RED
///       at the old head and GREEN after the catalog-only redesign.
///
/// RED UNDER A MUTANT (proved, recorded in the commit): restore gate 3's row-count read
/// (`SELECT EXISTS (SELECT 1 FROM vibe.documents LIMIT 1)`) and run it as the non-superuser owner -
/// C2 FAILS, because FORCE RLS filters the owner and the read reports empty, so the initializer would
/// re-provision. The catalog-only gate is what makes it GREEN.
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
    /// (no polling on a background task) and, for gate 1, proves the early return. The exit hook defaults
    /// to Environment.Exit; tests pass a throwing hook so a failure is an assertion, not a host death.</summary>
    private static async Task RunInitializerAsync(string? ownerConnectionString, CapturingLogger logger, Action<int>? exit = null)
    {
        var initializer = new VibeSchemaInitializer(ownerConnectionString, logger, exit);
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
    /// executes, so a skipped IF NOT EXISTS is still recorded. The log function is SECURITY DEFINER so it
    /// records even when the invoking role (a non-superuser owner in row C2) cannot write test_obs itself -
    /// otherwise the trigger would ERROR and the initializer's Environment.Exit(1) would crash the host
    /// instead of producing a clean assertion.</summary>
    private static async Task InstallDdlCaptureAsync(string cs)
    {
        await ExecAsync(cs, """
            CREATE SCHEMA IF NOT EXISTS test_obs;
            CREATE TABLE IF NOT EXISTS test_obs.ddl_log (id SERIAL PRIMARY KEY, tag TEXT NOT NULL, at TIMESTAMPTZ DEFAULT NOW());
            CREATE OR REPLACE FUNCTION test_obs.log_ddl() RETURNS event_trigger LANGUAGE plpgsql SECURITY DEFINER AS $$
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

    // ── C. set + vibe.documents present WITHOUT our marker -> NOT OURS: zero DDL, no marker ──────

    [Fact]
    public async Task C_a_documents_table_without_our_marker_is_not_ours_zero_ddl()
    {
        var cs = await CreateDatabaseAsync($"guard_notours_{Guid.NewGuid():N}");

        // Provision for real once (so we have the true schema + marker), then remove the marker and put
        // a tenant row in. This is exactly the state a live database left by someone else is in.
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

        logger.Entries.Should().Contain(e => e.Message.Contains("NO DDL will run") || e.Message.Contains("NOT created by this initializer"),
            "gate 4 must log that the database is not ours");
        (await DdlCountAsync(cs)).Should().Be(0,
            "BAPert's hard rule: an existing vibe.documents without our marker is not ours - ZERO DDL");
        (await MarkerExistsAsync(cs)).Should().BeFalse(
            "the marker must NOT be written when we refuse to provision");
        (await VibeIndexCountAsync(cs)).Should().Be(indexesBefore, "no index may be created or dropped");
        (await ScalarAsync<long>(cs,
            "SELECT count(*) FROM vibe.documents WHERE collection = 'agent_mail';"))
            .Should().Be(1, "the existing row must be untouched");
    }

    // ── C2. MUST-1 (NightHawk 65322): the RLS hole. A NON-SUPERUSER owner + FORCE RLS. ─────────────

    [Fact]
    public async Task C2_non_superuser_owner_with_force_rls_still_sees_not_ours_zero_ddl()
    {
        var db = $"guard_rls_{Guid.NewGuid():N}";

        // CREATE DATABASE and the test-only DDL capture must run as the container SUPERUSER.
        var adminCs = await CreateDatabaseAsync(db);

        // A dedicated, NON-superuser owner role - what a compose/.env install or an operator-set
        // OwnerConnectionString would actually use. This is the role NightHawk's measurement used.
        const string role = "vibe_owner";
        const string pw = "vibe_owner_pw";
        await ExecAsync(adminCs, $"CREATE ROLE {role} LOGIN PASSWORD '{pw}';");
        await ExecAsync(adminCs, $"GRANT ALL ON DATABASE \"{db}\" TO {role};");
        await ExecAsync(adminCs, $"GRANT ALL ON SCHEMA public TO {role};");

        var ownerCs = new NpgsqlConnectionStringBuilder(adminCs)
        {
            Username = role,
            Password = pw
        }.ConnectionString;

        // Provision AS THE OWNER, so every object is owned by the non-superuser role (FORCE RLS then
        // applies to it). This is the realistic install.
        var first = new CapturingLogger();
        await RunInitializerAsync(ownerCs, first);

        // Tenant rows inserted by the SUPERUSER (bypasses RLS), then delete the marker.
        await ExecAsync(adminCs, """
            INSERT INTO vibe.documents (client_id, collection, table_name, data)
            VALUES (1, 'agent_mail', 'agent_mail_inbox', '{"id":1}'::jsonb),
                   (8, 'agent_mail', 'agent_mail_inbox', '{"id":2}'::jsonb);
            """);
        await ExecAsync(adminCs, "DELETE FROM vibe.schema_meta WHERE key = 'initialized';");

        // Sanity: as the owner with app.client_id unset, the OLD row-count read is BLIND (sees no rows)
        // but the CATALOG read is correct (table exists). This is the hole MUST-1 named.
        await using (var owner = new NpgsqlConnection(ownerCs))
        {
            await owner.OpenAsync();
            await using var blind = new NpgsqlCommand("SELECT EXISTS (SELECT 1 FROM vibe.documents LIMIT 1);", owner);
            var seesRow = (bool)(await blind.ExecuteScalarAsync())!;
            seesRow.Should().BeFalse(
                "MUST-1: with FORCE RLS and app.client_id unset, the owner's row-count read sees NOTHING " +
                "even though the table holds tenant rows - which is exactly why the gate must not use it");
        }

        // The DDL capture must be created by the superuser and readable by the test connection (adminCs).
        await InstallDdlCaptureAsync(adminCs);
        await ResetDdlLogAsync(adminCs);

        var indexesBefore = await VibeIndexCountAsync(adminCs);
        var logger = new CapturingLogger();

        // Run the initializer AS THE OWNER (what a real install does).
        await RunInitializerAsync(ownerCs, logger);

        logger.Entries.Should().Contain(e => e.Message.Contains("NO DDL will run") || e.Message.Contains("NOT created by this initializer"),
            "the catalog gate must catch this: vibe.documents exists, marker absent -> not ours");
        (await DdlCountAsync(adminCs)).Should().Be(0,
            "MUST-1: with a non-superuser owner under FORCE RLS the initializer must STILL run zero DDL");
        (await MarkerExistsAsync(adminCs)).Should().BeFalse("no marker may be written");
        (await VibeIndexCountAsync(adminCs)).Should().Be(indexesBefore, "no index may be created or dropped");
    }

    // ── D. MUST (NightHawk 65348 M2, BAPert 65351 item 2): two initializers on one fresh DB ────────

    [Fact]
    public async Task D_two_concurrent_initializers_on_a_fresh_db_provision_exactly_once()
    {
        var cs = await CreateDatabaseAsync($"guard_tworeplicas_{Guid.NewGuid():N}");

        // Injectable exit hook: if either initializer hits the Environment.Exit path (as the loser would
        // WITHOUT the advisory lock), throw so this test FAILS with a clear message instead of the test
        // host disappearing - that is what makes the row a clean RED under the lock-removal mutant.
        var exited = new ConcurrentQueue<int>();
        void Exit(int code)
        {
            exited.Enqueue(code);
            throw new InvalidOperationException(
                "VIBE_INIT_EXIT: an initializer called the exit hook (code " + code +
                ") - without the advisory lock the loser races the winner into a duplicate-object failure (NightHawk 65348 M2)");
        }

        var logs = new ConcurrentQueue<CapturingLogger>();
        var l1 = new CapturingLogger();
        var l2 = new CapturingLogger();
        logs.Enqueue(l1);
        logs.Enqueue(l2);

        // Two replicas starting together.
        await Task.WhenAll(
            Task.Run(() => RunInitializerAsync(cs, l1, Exit)),
            Task.Run(() => RunInitializerAsync(cs, l2, Exit)));

        exited.Should().BeEmpty("neither replica may hit the exit path - the advisory lock makes the loser a no-op");
        (await MarkerExistsAsync(cs)).Should().BeTrue("exactly one provisioning writes the marker");
        (await SchemaExistsAsync(cs)).Should().BeTrue();

        // Exactly ONE 'schema created' log across both replicas; the other takes the no-op path.
        var createdCount = new[] { l1, l2 }
            .SelectMany(l => l.Entries)
            .Count(e => e.Message.Contains("vibe schema created"));
        createdCount.Should().Be(1,
            "with pg_advisory_xact_lock the loser blocks, then sees the committed table + marker and no-ops; " +
            "without it BOTH try to create and one dies on a duplicate-object error (RED under the mutant)");
    }
}
