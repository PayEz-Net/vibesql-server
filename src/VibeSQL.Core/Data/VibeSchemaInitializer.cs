using System.Reflection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace VibeSQL.Core.Data;

/// <summary>
/// PAY-1865 (Jon 15:45 via rigpert 65251; BAPert 65257/65265): self-provision the <c>vibe</c> schema on
/// a FRESH self-hosted install, and do NOTHING anywhere else.
///
/// WHY THIS IS GATED RATHER THAN ALWAYS-ON. It executes DDL, and the measurements that follow are why
/// the gate exists:
///   * The service connects as <c>vsql_server_user</c> (93/AKS), which has NO DDL rights. Running this
///     on the service connection fails and the catch calls Environment.Exit(1) -> crash-loop.
///   * Even a fully IDEMPOTENT script cannot run as that role: CREATE SCHEMA IF NOT EXISTS, ALTER TABLE
///     ... ENABLE ROW LEVEL SECURITY, CREATE POLICY and DROP POLICY IF EXISTS are all OWNER-only, and
///     PostgreSQL checks the privilege BEFORE the already-exists check (MEASURED 2026-09-27 on a
///     least-privilege role: "must be owner of table", exit 1, on an ALREADY-PROVISIONED database).
///   * <c>vibe.documents</c> is LIST-PARTITIONED and prod-sized, so a plain CREATE INDEX at boot would
///     LOCK it. BAPert's hard rule: on a POPULATED database the startup path executes NO DDL.
///
/// THE GATE, in order (REDESIGNED for MUST-1, NightHawk 65322 / BAPert 65327):
///   1. <c>VibeSql:Schema:OwnerConnectionString</c> unset -> SKIP entirely (no connection attempted).
///      This is the state of every AKS/93 deployment, so that image is unaffected. Logged once.
///   2. <c>vibe.documents</c> ABSENT (to_regclass IS NULL) -> genuinely FRESH -> provision.
///   3. <c>vibe.documents</c> PRESENT + the explicit marker row present -> already provisioned -> no-op.
///   4. <c>vibe.documents</c> PRESENT + NO marker -> NOT OURS: log at Error, run ZERO DDL, write NO
///      marker. A half-provisioned state cannot exist because provisioning is atomic (below), so an
///      existing table without our marker is somebody else's database.
///
/// *** WHY THE OLD GATE WAS WRONG (MUST-1): it read a ROW COUNT to decide emptiness, and that read is
/// blind. *** base-schema.v2 enables AND FORCES row-level security on vibe.documents, and FORCE filters
/// the table OWNER too. A gate-3 <c>SELECT EXISTS (SELECT 1 FROM vibe.documents LIMIT 1)</c> on the
/// owner connection, with app.client_id unset, sees only client_id = 0 rows - so a LIVE database with
/// tenant rows reads as EMPTY and gate 4 would run the whole schema (non-concurrent index builds on the
/// partitioned table = the lock BAPert's hard rule forbids). MEASURED (NightHawk, postgres:16, non-superuser
/// owner, 2 tenant rows): as superuser populated=true; as the owner populated=FALSE. Emptiness is now
/// decided from the CATALOG ONLY (does vibe.documents exist), so no row is ever read and RLS cannot
/// fool it. The marker read touches vibe.schema_meta, which is not tenant-scoped and not RLS-forced.
///
/// ATOMICITY (BAPert 65327 item 2): schema + seed + marker run in ONE transaction. The schema has no
/// CONCURRENTLY (correct on a fresh DB), so all of it is transactional DDL; a failure rolls the whole
/// thing back and no half-provisioned state exists.
///
/// CONCURRENCY (SHOULD-1 -> BAPert 65327 item 3): <c>pg_advisory_xact_lock</c> around the gates, held
/// for the transaction, so two replicas starting together cannot both provision.
///
/// The owner connection is used ONLY here and disposed when done. It is never registered in DI for
/// request paths.
///
/// CONSTRAINT INHERITED WITH THE DESIGN: Postgres runs a multi-statement command in an IMPLICIT
/// TRANSACTION, so CREATE INDEX CONCURRENTLY cannot appear in the schema script. The fresh-install
/// script therefore uses PLAIN CREATE INDEX (correct on an empty database, no lock to avoid). The
/// CONCURRENTLY forms for the live AKS/prod table live in PayEz-Core/AKS/migrations.
///
/// THE DDL IS NOT DUPLICATED HERE. It is loaded from the embedded <c>scripts/base-schema.v2.sql</c> so
/// the schema has exactly one source of truth.
/// </summary>
public sealed class VibeSchemaInitializer : BackgroundService
{
    private const string SchemaResourceName = "VibeSQL.Core.scripts.base-schema.v2.sql";

    /// <summary>Explicit "this database has been provisioned" marker (BAPert 65257: NOT the missing ledger alone).</summary>
    private const string MarkerSql = @"
        CREATE SCHEMA IF NOT EXISTS vibe;
        CREATE TABLE IF NOT EXISTS vibe.schema_meta (
            key         VARCHAR(64) PRIMARY KEY,
            value       TEXT,
            updated_at  TIMESTAMPTZ NOT NULL DEFAULT NOW()
        );
        INSERT INTO vibe.schema_meta (key, value)
        VALUES ('initialized', NOW()::text)
        ON CONFLICT (key) DO UPDATE SET value = EXCLUDED.value, updated_at = NOW();";

    private readonly string? _ownerConnectionString;
    private readonly ILogger<VibeSchemaInitializer> _logger;

    public VibeSchemaInitializer(string? ownerConnectionString, ILogger<VibeSchemaInitializer> logger)
    {
        _ownerConnectionString = ownerConnectionString;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // GATE 1: no owner connection -> skip. No connection is attempted.
        if (string.IsNullOrWhiteSpace(_ownerConnectionString))
        {
            _logger.LogInformation(
                "VIBESQL_SCHEMA_INIT: schema provisioning skipped: no owner connection " +
                "(VibeSql:Schema:OwnerConnectionString is unset). Pending schema work, if any, is applied " +
                "via AKS/migrations.");
            return;
        }

        try
        {
            await using var connection = new NpgsqlConnection(_ownerConnectionString);
            await connection.OpenAsync(stoppingToken);

            // MUST-1 (NightHawk 65322) / BAPert 65327: schema + seed + marker in ONE transaction, with
            // pg_advisory_xact_lock held for it, so two replicas cannot both provision and a failure
            // leaves NO half-provisioned state.
            await using var tx = await connection.BeginTransactionAsync(stoppingToken);

            await using (var lockCmd = new NpgsqlCommand(
                "SELECT pg_advisory_xact_lock(hashtext('vibesql_schema_init'));", connection, tx))
            {
                await lockCmd.ExecuteNonQueryAsync(stoppingToken);
            }

            // GATES 2-3: emptiness is CATALOG-ONLY. No row is ever read, so FORCE RLS cannot make a
            // populated database look empty (MUST-1).
            var documentsExists = await TableExistsAsync(connection, tx, "vibe", "documents", stoppingToken);

            if (documentsExists)
            {
                if (await IsMarkedInitializedAsync(connection, tx, stoppingToken))
                {
                    // GATE 3: already provisioned -> no-op.
                    await tx.RollbackAsync(stoppingToken);
                    _logger.LogInformation(
                        "VIBESQL_SCHEMA_INIT: database already provisioned (vibe.schema_meta marker present); nothing to do.");
                    return;
                }

                // GATE 4: vibe.documents exists WITHOUT our marker -> NOT OURS. Zero DDL, no marker.
                await tx.RollbackAsync(stoppingToken);
                _logger.LogError(
                    "VIBESQL_SCHEMA_INIT: vibe.documents EXISTS but the initialization marker is absent - " +
                    "this database was NOT created by this initializer. NO DDL will run and no marker will be " +
                    "written. Provision it deliberately, or apply pending schema changes via AKS/migrations.");
                return;
            }

            // GATE 2: vibe.documents ABSENT -> genuinely FRESH -> provision, all in this transaction.
            var schemaSql = LoadEmbeddedSchema();
            await using (var command = new NpgsqlCommand(schemaSql, connection, tx))
            {
                await command.ExecuteNonQueryAsync(stoppingToken);
            }
            _logger.LogInformation("VIBESQL_SCHEMA_INIT: vibe schema created (tables, RLS, indexes) on a fresh database");

            await using (var seed = new NpgsqlCommand(SystemUserSeedSql, connection, tx))
            {
                var seeded = await seed.ExecuteNonQueryAsync(stoppingToken);
                _logger.LogInformation("VIBESQL_SCHEMA_INIT: system user seed complete ({Seeded} tenant(s) provisioned)", seeded);
            }

            await using (var mark = new NpgsqlCommand(MarkerSql, connection, tx))
            {
                await mark.ExecuteNonQueryAsync(stoppingToken);
            }

            await tx.CommitAsync(stoppingToken);
            _logger.LogInformation("VIBESQL_SCHEMA_INIT: initialization marker written (vibe.schema_meta.initialized)");
        }
        catch (Exception ex)
        {
            // Fail fast ONLY when we were asked to provision. A skip (gate 1, 3, 4) never reaches here.
            _logger.LogError(ex, "VIBESQL_SCHEMA_INIT: failed to initialize vibe schema; failing fast");
            Environment.Exit(1);
        }
    }

    /// <summary>Catalog-only table existence (pg_class). Does not read any row, so RLS cannot affect it.</summary>
    private static async Task<bool> TableExistsAsync(
        NpgsqlConnection connection, NpgsqlTransaction tx, string schema, string table, CancellationToken ct)
    {
        const string sql = @"
            SELECT EXISTS (
                SELECT 1 FROM pg_catalog.pg_class c
                  JOIN pg_catalog.pg_namespace n ON n.oid = c.relnamespace
                 WHERE n.nspname = @schema AND c.relname = @table
            );";
        await using var cmd = new NpgsqlCommand(sql, connection, tx);
        cmd.Parameters.AddWithValue("schema", schema);
        cmd.Parameters.AddWithValue("table", table);
        return (bool)(await cmd.ExecuteScalarAsync(ct) ?? false);
    }

    /// <summary>True when the explicit marker row exists. Reads vibe.schema_meta, which is NOT
    /// tenant-scoped and carries no RLS policy - so this read is unaffected by MUST-1's FORCE-RLS hole.</summary>
    private static async Task<bool> IsMarkedInitializedAsync(
        NpgsqlConnection connection, NpgsqlTransaction tx, CancellationToken ct)
    {
        if (!await TableExistsAsync(connection, tx, "vibe", "schema_meta", ct))
            return false;

        await using var read = new NpgsqlCommand(
            "SELECT EXISTS (SELECT 1 FROM vibe.schema_meta WHERE key = 'initialized');", connection, tx);
        return (bool)(await read.ExecuteScalarAsync(ct) ?? false);
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
    /// Ensures every tenant that has users also has a <c>type='system'</c> user. (Unchanged from the
    /// original; see git history for the full rationale on the reserved id and the per-tenant scope.)
    /// </summary>
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
