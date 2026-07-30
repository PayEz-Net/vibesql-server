# VibeSQL Schema Sentinel — Implementation Decisions

**Date:** 2026-04-01  
**Reviewer:** QAPert  
**Status:** DECISIONS MADE — Ready for Implementation

---

## Blocking Issues Resolved

### Q2: Override Mechanism for Destructive Changes ✅ DECIDED

**Decision:** Header-based override: `X-Vibe-Force-Schema-Update: true`

```csharp
// In SchemasController.Put()
var forceOverride = Request.Headers.TryGetValue("X-Vibe-Force-Schema-Update", out var forceHeader)
    && forceHeader.ToString().Equals("true", StringComparison.OrdinalIgnoreCase)
    && verdict.Verdict == SentinelVerdict.Destructive; // NEVER for Prohibited

if (verdict.Blocked && !forceOverride)
{
    return StatusCode(409, new { ... });
}

if (forceOverride)
{
    _logger.LogWarning("SENTINEL_OVERRIDE: Destructive change forced. Collection={Collection}, RiskItems={Count}",
        collection, verdict.RiskReport.Count);
}
```

**Why header not query param:** PUT must remain idempotent. Query params affect cache keys.

**Critical rule:** Prohibited (P-400) can NEVER be overridden. Only Destructive (D-300).

---

### Q3: Inspector Fallback When No IDataInspector Registered ✅ DECIDED

**Decision:** Option A — Assume worst case (BLOCK all D-300)

```csharp
// In SentinelPipeline.AnalyzeAsync()
DataInspectionResult? inspection = null;
if (classification.RequiresDataCheck)
{
    if (_inspector == null)
    {
        // SAFETY FIRST: No inspector = cannot downgrade = remain blocked
        _logger?.LogWarning("SENTINEL_NO_INSPECTOR: Destructive changes blocked (no IDataInspector registered)");
        // Return without downgrade
        return SentinelVerdictResult.FromInspection(diff, classification, null, classification.Verdict);
    }
    
    inspection = await _inspector.InspectAsync(classification.DataCheckItems, ct);
    // ... downgrade logic
}
```

**Documented behavior:**
> If no `IDataInspector` is registered, Destructive changes cannot be downgraded and will remain blocked. This is the only defensible default for a schema protection system.

---

### Q5: M-200 Migration Execution Model ✅ DECIDED

**Decision:** Synchronous with timeout protection. Background jobs out of scope for v1.

```csharp
// Configuration
public class VibeSentinelOptions
{
    public bool Enabled { get; set; } = true;
    public bool BlockDestructive { get; set; } = true;
    public bool BlockProhibited { get; set; } = true;
    public int MigrationTimeoutSeconds { get; set; } = 30;  // NEW
    public int InspectorBudgetMs { get; set; } = 500;
    public int PerQueryTimeoutMs { get; set; } = 5000;
}
```

**M-200 handling in controller:**
```csharp
if (verdict.Verdict == SentinelVerdict.Migration)
{
    _logger.LogInformation("SENTINEL_MIGRATION: Auto-applying DDL for {Count} changes", 
        verdict.Classification?.Items.Count(i => i.Level == SentinelVerdict.Migration));
    
    // DDL execution is synchronous with timeout
    // If it fails, return 500 with SENTINEL_MIGRATION_FAILED
}
```

**Rationale:** 
- M-200 = "safe" migrations (add column with default, widen type)
- These are fast operations in PostgreSQL
- 30s timeout is generous for any reasonable migration
- Background jobs add complexity (job queue, polling, state management)
- **Defer async migrations to v2** if needed

---

## Additional Decisions

### Q1: Run Sentinel on CREATE or only UPDATE? ✅ DECIDED

**Decision:** Run on CREATE with structural validation only

```csharp
// In SchemasController.Put()
if (currentDoc == null)
{
    // CREATE: Structural validation only (no diff)
    var structuralItems = ValidateStructuralConstraints(proposedDoc, collection);
    if (structuralItems.Any(i => i.Level == SentinelVerdict.Prohibited))
    {
        return StatusCode(422, new { ... });
    }
    // Skip to insert (no migration needed for CREATE)
}
else
{
    // UPDATE: Full Sentinel pipeline
    var verdict = await pipeline.AnalyzeAsync(currentDoc, proposedDoc, context, ct);
    // ... handle verdict
}

private List<SentinelItem> ValidateStructuralConstraints(JsonDocument schema, string collection)
{
    var items = new List<SentinelItem>();
    
    // P-403: System collection guard
    if (collection.StartsWith("vibe_") || collection.StartsWith("system_"))
    {
        items.Add(new SentinelItem(
            SentinelTaxonomy.P403_ModifySystemSchema,
            SentinelVerdict.Prohibited,
            "Cannot create system collection schema via API"));
    }
    
    // Structural: Detect if schema has any tables
    if (!HasTables(schema))
    {
        items.Add(new SentinelItem(
            "V-001",  // Validation code
            SentinelVerdict.Prohibited,
            "Schema must contain at least one table"));
    }
    
    return items;
}
```

---

### Q4: Multiple Inspector Support? ✅ DECIDED

**Decision:** Last registration wins (keep it simple)

```csharp
// Consumer composes if needed:
services.AddSingleton<IDataInspector>(sp => 
    new CompositeInspector(new[]
    {
        new PostgresTableInspector(...),
        new CustomInspector(...)
    }));

public class CompositeInspector : IDataInspector
{
    private readonly IEnumerable<IDataInspector> _inspectors;
    // Runs all, aggregates results
}
```

---

### Q6: Configuration Wiring ✅ DECIDED

**Decision:** Add `VibeSentinelOptions` class and wire through IOptions

```csharp
// VibeSQL.Core/Options/VibeSentinelOptions.cs
public class VibeSentinelOptions
{
    public bool Enabled { get; set; } = true;
    public bool BlockDestructive { get; set; } = true;
    public bool BlockProhibited { get; set; } = true;
    public int MigrationTimeoutSeconds { get; set; } = 30;
    public int InspectorBudgetMs { get; set; } = 500;
    public int PerQueryTimeoutMs { get; set; } = 5000;
    public bool RequireInspectorForDestructive { get; set; } = true;  // Q3 behavior
}

// Program.cs
builder.Services.Configure<VibeSentinelOptions>(
    builder.Configuration.GetSection("VibeSQL:Sentinel"));

// DI Extension
public static IServiceCollection AddVibeSentinelServices(
    this IServiceCollection services, IConfiguration configuration)
{
    services.Configure<VibeSentinelOptions>(
        configuration.GetSection("VibeSQL:Sentinel"));
    
    services.AddSingleton<ISchemaDiffEngine, SchemaDiffEngine>();
    services.AddSingleton<IChangeClassifier, ChangeClassifier>();
    services.TryAddSingleton<IDataInspector, PostgresTableInspector>();
    
    return services;
}
```

---

### Q7: JsonDocument.Parse NRE Protection ✅ DECIDED

**Decision:** Validate before parse

```csharp
// In SchemasController.Put()
if (string.IsNullOrWhiteSpace(request.JsonSchema))
    return BadRequest(ErrorResponse("MISSING_REQUIRED_FIELD", "json_schema is required"));

JsonDocument proposedDoc;
try
{
    proposedDoc = JsonDocument.Parse(request.JsonSchema);
}
catch (JsonException ex)
{
    return BadRequest(ErrorResponse("INVALID_JSON", $"Invalid JSON schema: {ex.Message}"));
}

// Use 'using' for disposal
using (proposedDoc)
{
    // ... pipeline logic
}
```

---

## Updated API Response Contracts

### 202 Accepted — Migration (M-200) with Background Job

**NOT IMPLEMENTED IN V1** — All migrations are synchronous.

If we add async migrations later:

```json
{
  "success": true,
  "data": {
    "migrationId": "mig_abc123",
    "status": "pending",
    "estimatedDuration": "30s"
  },
  "meta": {
    "sentinel": {
      "verdict": "Migration",
      "codes": ["M-200"]
    }
  }
}
```

For v1, M-200 returns standard 200 with synchronous completion.

---

## Implementation Checklist

### Phase 1: Core Integration
- [ ] Add `VibeSentinelOptions` class
- [ ] Update `AddVibeSentinelServices` extension method
- [ ] Wire options in `Program.cs`
- [ ] Update `SentinelPipeline` to handle null inspector (Q3)
- [ ] Update `PostgresTableInspector` to use options (not hardcoded values)

### Phase 2: Controller Integration
- [ ] Add JSON validation before `JsonDocument.Parse` (Q7)
- [ ] Add CREATE vs UPDATE branching (Q1)
- [ ] Integrate full Sentinel pipeline for UPDATE
- [ ] Add `X-Vibe-Force-Schema-Update` header check (Q2)
- [ ] Add logging for all Sentinel decisions

### Phase 3: Testing
- [ ] Unit tests: `SentinelPipeline` with null inspector
- [ ] Unit tests: `ChangeClassifier` edge cases
- [ ] Integration test: Safe change (S-100) → 200
- [ ] Integration test: Migration (M-200) → 200
- [ ] Integration test: Destructive empty table (D-300) → 200 (downgraded)
- [ ] Integration test: Destructive with data (D-300) → 409
- [ ] Integration test: Destructive with override → 200
- [ ] Integration test: Prohibited (P-400) → 422 (no override)
- [ ] Integration test: Invalid JSON → 400

### Phase 4: Documentation
- [ ] Update Swagger docs with Sentinel responses
- [ ] Add `X-Vibe-Force-Schema-Update` to security schemes
- [ ] Update README with Sentinel feature description

---

## Files to Modify

| File | Changes |
|------|---------|
| `VibeSQL.Core/Options/VibeSentinelOptions.cs` | NEW — Configuration class |
| `VibeSQL.Core/ServiceCollectionExtensions.cs` | UPDATE — Add options wiring |
| `VibeSQL.Core/Sentinel/PostgresTableInspector.cs` | UPDATE — Use options |
| `VibeSQL.Sentinel/SentinelPipeline.cs` | UPDATE — Handle null inspector |
| `VibeSQL.Server/Controllers/SchemasController.cs` | UPDATE — Integrate Sentinel |
| `VibeSQL.Server/Program.cs` | UPDATE — Wire up services |
| `VibeSQL.Server/appsettings.json` | UPDATE — Add config section |

---

## Sign-off

| Question | Decision | Risk Level |
|----------|----------|------------|
| Q1: CREATE validation | Structural only | Low |
| Q2: Override mechanism | `X-Vibe-Force-Schema-Update` header | Low |
| Q3: Null inspector fallback | BLOCK all D-300 | Low |
| Q4: Multiple inspectors | Last wins | Low |
| Q5: M-200 execution | Synchronous (30s timeout) | Medium* |
| Q6: Configuration | `VibeSentinelOptions` + IOptions | Low |
| Q7: JSON validation | Validate before parse | Low |

*Medium risk: Large migrations could timeout. Mitigation: 30s is generous for DDL, document limitation.

**Verdict:** READY FOR IMPLEMENTATION
