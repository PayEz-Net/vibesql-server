using System.Reflection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace VibeSQL.Core.Data;

/// <summary>
/// Ensures the <c>vibe</c> schema, its eight product tables, indexes, row-level
/// security and the per-tenant system user exist on startup.
/// </summary>
/// <remarks>
/// Pattern borrowed from vsql-cache's SchemaInitializer: a BackgroundService that
/// runs idempotent multi-statement DDL through a single NpgsqlCommand and fails
/// fast. Two constraints inherited with it, both load-bearing:
///
///   * Postgres runs a multi-statement command in an IMPLICIT TRANSACTION, so
///     CREATE INDEX CONCURRENTLY cannot appear in the script — it throws, the
///     catch calls Environment.Exit(1), and the pod crash-loops. Changing an
///     index definition needs a real migration, not an edit here.
///   * Every statement must be IF NOT EXISTS / guarded. This runs on every boot.
///
/// THE DDL IS NOT DUPLICATED HERE. It is loaded from the embedded
/// scripts/base-schema.v2.sql so the schema has exactly one source of truth.
/// A C# copy of the same tables would drift from the .sql file the moment either
/// changed, which is the defect class this codebase has repeatedly been bitten by
/// (see cards 186220, 186212): a declaration that looks authoritative while
/// something else is actually in force.
/// </remarks>
public sealed class VibeSchemaInitializer : BackgroundService
{
    private const string SchemaResourceName = "VibeSQL.Core.scripts.base-schema.v2.sql";

    private readonly string _connectionString;
    private readonly ILogger<VibeSchemaInitializer> _logger;

    public VibeSchemaInitializer(string connectionString, ILogger<VibeSchemaInitializer> logger)
    {
        _connectionString = connectionString;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            var schemaSql = LoadEmbeddedSchema();

            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(stoppingToken);

            await using (var command = new NpgsqlCommand(schemaSql, connection))
            {
                await command.ExecuteNonQueryAsync(stoppingToken);
            }
            _logger.LogInformation("VIBESQL_SCHEMA_INIT: vibe schema ensured (8 tables, RLS, indexes)");

            await using (var seed = new NpgsqlCommand(SystemUserSeedSql, connection))
            {
                var seeded = await seed.ExecuteNonQueryAsync(stoppingToken);
                _logger.LogInformation("VIBESQL_SCHEMA_INIT: system user seed complete ({Seeded} tenant(s) provisioned)", seeded);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "VIBESQL_SCHEMA_INIT: failed to initialize vibe schema; failing fast");
            Environment.Exit(1);
        }
    }

    private static string LoadEmbeddedSchema()
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(SchemaResourceName)
            ?? throw new InvalidOperationException(
                $"Embedded schema '{SchemaResourceName}' not found. It must be declared as an " +
                "EmbeddedResource in VibeSQL.Core.csproj — a missing script must fail loudly here, " +
                "not silently start a server against an unprovisioned database.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>
    /// Ensures every tenant that has users also has a <c>type='system'</c> user.
    /// </summary>
    /// <remarks>
    /// WHY THIS EXISTS: audit rows carry a NOT NULL admin_user_id that must resolve to a
    /// real user in the SAME tenant. Operations with no authenticated caller — scheduled
    /// jobs, migrations, system-initiated writes — have nobody to name. Without a system
    /// user those audit rows cannot be written at all, so the audit trail silently loses
    /// exactly the events least likely to have a human behind them.
    ///
    /// Measured 2026-08-08: of three tenants holding users, only client 9 had one.
    ///
    /// user_id = 1 is the reserved system id, taken when free. It is not hardcoded blindly:
    /// user_id is unique PER TENANT (user_id 1001 exists under two different clients as two
    /// different people), so a tenant where 1 is already occupied gets max+1 instead.
    ///
    /// idp_user_id is deliberately omitted. A service account has no IDP identity, and the
    /// collection's `required` list is enforced at the API layer, not here — this seed writes
    /// the row directly, which is the same path by which the original client 9 system user
    /// was created. That asymmetry is intentional but worth knowing: you cannot create a
    /// system user through the public API today.
    /// </remarks>
    private const string SystemUserSeedSql = @"
        DO $$
        DECLARE
            t          RECORD;
            new_id     INT;
        BEGIN
            FOR t IN
                SELECT DISTINCT client_id
                FROM vibe.documents
                WHERE collection = 'vibe_app' AND table_name = 'users' AND deleted_at IS NULL
            LOOP
                IF NOT EXISTS (
                    SELECT 1 FROM vibe.documents
                    WHERE client_id = t.client_id
                      AND collection = 'vibe_app' AND table_name = 'users'
                      AND deleted_at IS NULL
                      AND data->>'type' = 'system'
                ) THEN
                    -- Reserved id 1 when free; otherwise the next free id in THIS tenant.
                    SELECT CASE
                             WHEN EXISTS (
                               SELECT 1 FROM vibe.documents
                               WHERE client_id = t.client_id
                                 AND collection = 'vibe_app' AND table_name = 'users'
                                 AND deleted_at IS NULL
                                 AND (data->>'user_id')::int = 1)
                             THEN COALESCE(MAX((data->>'user_id')::int), 0) + 1
                             ELSE 1
                           END
                      INTO new_id
                      FROM vibe.documents
                     WHERE client_id = t.client_id
                       AND collection = 'vibe_app' AND table_name = 'users'
                       AND deleted_at IS NULL;

                    INSERT INTO vibe.documents
                        (client_id, collection, table_name, data, created_at, created_by)
                    VALUES (
                        t.client_id, 'vibe_app', 'users',
                        jsonb_build_object(
                            'name',  'vibe_app_system',
                            'type',  'system',
                            'email', 'system@vibe_app.vibe',
                            'user_id', new_id,
                            'created_by', new_id,
                            'updated_by', new_id
                        ),
                        now(), new_id);

                    RAISE NOTICE 'VIBESQL_SCHEMA_INIT: seeded system user_id=% for client_id=%', new_id, t.client_id;
                END IF;
            END LOOP;
        END $$;";
}
