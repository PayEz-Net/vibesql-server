# VibeSQL Schema Sentinel (VS-SS) Implementation Status

**Document Version:** 1.0  
**Spec Version:** VS-SS v0.3.1  
**Date:** 2026-04-01

---

## Executive Summary

The VibeSQL Schema Sentinel (VS-SS) is a **structural schema diff + classification engine** designed to prevent destructive schema changes in production. It follows a strict taxonomy (S-100/M-200/D-300/P-400) and provides deterministic verdicts for every schema change.

### Current Status: **CORE ENGINE COMPLETE, INTEGRATION PENDING**

| Component | Status | Location |
|-----------|--------|----------|
| Taxonomy & Types | ✅ Complete | `VibeSQL.Sentinel/SentinelTaxonomy.cs` |
| Schema Diff Engine | ✅ Complete | `VibeSQL.Sentinel/SchemaDiffEngine.cs` |
| Change Classifier | ✅ Complete | `VibeSQL.Sentinel/ChangeClassifier.cs` |
| Sentinel Pipeline | ✅ Complete | `VibeSQL.Sentinel/SentinelPipeline.cs` |
| Postgres Inspector | ✅ Complete | `VibeSQL.Core/Sentinel/PostgresTableInspector.cs` |
| **Controller Integration** | ❌ **MISSING** | `VibeSQL.Server/Controllers/SchemasController.cs` |
| **DI Registration** | ❌ **MISSING** | `VibeSQL.Server/Program.cs` |

---

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────────────┐
│                    SCHEMA CHANGE REQUEST                            │
│  PUT /v1/schemas/{collection}  { json_schema: {...} }               │
└─────────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────────┐
│  STEP 1: STRUCTURAL VALIDATION                                      │
│  - JSON parseable                                                   │
│  - Has "tables" or "properties"                                     │
│  - Reserved name checks (vibe_*, system_*)                          │
└─────────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────────┐
│  STEP 2: SCHEMA DIFF                                                │
│  SchemaDiffEngine.ComputeDiff(oldSchema, newSchema)                 │
│  - Tables added/removed                                             │
│  - Columns added/removed/modified                                   │
└─────────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────────┐
│  STEP 3: CLASSIFICATION                                             │
│  ChangeClassifier.Classify(diff, context)                           │
│  - Maps each change to taxonomy code                                │
│  - Calculates overall verdict (Level 0-4)                           │
└─────────────────────────────────────────────────────────────────────┘
                              │
                              ▼ (if verdict >= Destructive)
┌─────────────────────────────────────────────────────────────────────┐
│  STEP 4: DATA INSPECTION (Optional)                                 │
│  IDataInspector.InspectAsync(items)                                 │
│  - D-300: Count rows before DROP TABLE                              │
│  - D-301: Count non-null values before DROP COLUMN                  │
│  - D-306: Count NULLs before NOT NULL constraint                    │
│  - Empty table → downgrade to Migration                             │
└─────────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────────┐
│  STEP 5: VERDICT                                                    │
│  ┌─────────────┬──────────┬────────────────────────────────────────┐│
│  │ Level 0     │ NoChange │ 200 OK - Nothing to do                 ││
│  │ Level 1     │ Safe     │ 200 OK - Auto-apply                    ││
│  │ Level 2     │ Migration│ 200 OK - Auto-apply with DDL           ││
│  │ Level 3     │Destructive│ 409 CONFLICT - Blocked (can override) ││
│  │ Level 4     │Prohibited│ 422 UNPROCESSABLE - Never allowed      ││
│  └─────────────┴──────────┴────────────────────────────────────────┘│
└─────────────────────────────────────────────────────────────────────┘
```

---

## Component Details

### 1. SentinelTaxonomy (S-100/M-200/D-300/P-400)

```csharp
// Level 1: SAFE (auto-apply)
S-100  AddTable                    // New table
S-101  AddNullableColumn           // Nullable column
S-102  AddColumnWithDefault        // Non-null with default
S-103  ExpandTextLength            // varchar(50) → varchar(100)
S-104  LoosenConstraint            // NOT NULL → NULL
S-105  AddEnumValue                // Enum extension
S-106  AddIndex                    // New index
S-107  AddCheckNotValid            // CHECK constraint (not valid)
S-108  ChangeComment               // Metadata only
S-109  ChangeDefaultNullable       // Default value change

// Level 2: MIGRATION (auto-apply with DDL)
M-200  AddNonNullWithDefault       // Requires backfill
M-201  AddFkConstraint             // Foreign key
M-202  AddUniqueNoDupes            // Unique (no duplicates)
M-203  NullableToNonNullNoNulls    // Safe constraint add
M-204  WidenColumnType             // int → bigint
M-205  AddTableWithFk              // Table + FK together

// Level 3: DESTRUCTIVE (blocked, requires override)
D-300  DropTable                   // Table removal
D-301  DropColumn                  // Column removal
D-302  NarrowColumnType            // varchar(100) → varchar(50)
D-303  IncompatibleTypeCast        // text → integer (unsafe)
D-304  RenameTable                 // Name change = drop + add
D-305  RenameColumn                // Name change = drop + add
D-306  NullableToNonNullHasNulls   // Would violate existing rows
D-307  AddUniqueHasDupes           // Would violate existing rows
D-308  DropFkConstraint            // Referential integrity
D-309  DropPkConstraint            // Primary key
D-310  DropCheckConstraint         // Data quality
D-311  TightenConstraint           // Making things stricter
D-312  FullReplaceRegression       // Schema regression

// Level 4: PROHIBITED (never allowed, no override)
P-400  DropEntireSchema            // Complete removal
P-401  MajorTableRegression        // >50% tables removed
P-402  ChangePkType                // Primary key type change
P-403  ModifySystemSchema          // System collection guard
P-404  DropTableWithFkDependents   // Referential violation
```

### 2. SchemaDiffEngine

Pure in-memory comparison. No database access.

```csharp
public class SchemaDiffEngine : ISchemaDiffEngine
{
    public SchemaDiff ComputeDiff(JsonDocument? oldSchema, JsonDocument newSchema)
    {
        // Returns:
        // - TablesAdded: List<string>
        // - TablesRemoved: List<string>
        // - ColumnsAdded: Dictionary<table, List<ColumnDef>>
        // - ColumnsRemoved: Dictionary<table, List<ColumnDef>>
        // - ColumnsModified: Dictionary<table, List<ColumnModification>>
    }
}
```

**Key Design Decisions:**
- Case-insensitive table/column name comparison
- Supports both multi-table (`{"tables": {...}}`) and legacy single-table formats
- Extracts type, nullable, and default from JSON Schema properties

### 3. ChangeClassifier

Deterministic rules engine. Same input → same output, always.

```csharp
public class ChangeClassifier : IChangeClassifier
{
    public ClassificationResult Classify(SchemaDiff diff, ClassifierContext? context)
    
    // Context provides:
    // - ExistingTableCount (for P-401 detection)
    // - IsSystemCollection (for P-403 guard)
}
```

**Re-evaluation Logic:**
- Data inspector can downgrade Destructive → Migration if no data at risk
- Never upgrades (conservative by design)
- Error = block (never auto-allow when uncertain)

### 4. PostgresTableInspector

For real PostgreSQL tables (hard tables, not JSONB).

```csharp
public class PostgresTableInspector : IDataInspector
{
    // Budget constraints (spec requirements):
    private const long ExactCountThreshold = 100_000;  // Use pg_class above this
    private static readonly TimeSpan TotalBudget = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan PerQueryTimeout = TimeSpan.FromSeconds(5);
    
    // Timeout/budget exceeded = BLOCK (assume destructive)
}
```

**Query Strategy:**
1. Get approximate count from `pg_class.reltuples`
2. If small (< 100k), get exact `COUNT(*)`
3. If large, trust approximation
4. Cancel queries exceeding 5s per query, 500ms total

### 5. SentinelPipeline

Orchestrates the entire flow.

```csharp
public class SentinelPipeline
{
    public async Task<SentinelVerdictResult> AnalyzeAsync(
        JsonDocument? currentSchema,
        JsonDocument proposedSchema,
        ClassifierContext? context = null)
}

// Result provides:
// - Verdict: SentinelVerdict enum (0-4)
// - AutoApply: true if verdict <= Migration
// - Blocked: true if verdict >= Destructive
// - RiskReport: Detailed items for 409/422 response body
```

---

## Missing Integration Points

### 1. DI Registration (Program.cs)

```csharp
// CURRENT: Missing entirely
// REQUIRED:
builder.Services.AddVibeSentinelServices();

// Extension method in VibeSQL.Core:
public static IServiceCollection AddVibeSentinelServices(this IServiceCollection services)
{
    services.AddSingleton<ISchemaDiffEngine, SchemaDiffEngine>();
    services.AddSingleton<IChangeClassifier, ChangeClassifier>();
    
    // Inspector is optional - injected if available
    services.TryAddSingleton<IDataInspector, PostgresTableInspector>();
    
    return services;
}
```

### 2. SchemasController Integration

**Current Code (line 91-177 in SchemasController.cs):**
```csharp
[HttpPut("schemas/{collection}")]
public async Task<IActionResult> CreateOrUpdateSchema(...)
{
    // Validates... then directly inserts:
    var insertSql = @"INSERT INTO vibe.collection_schemas (...) VALUES (...)";
    // NO SENTINEL CHECK!
}
```

**Required Integration:**
```csharp
[HttpPut("schemas/{collection}")]
public async Task<IActionResult> CreateOrUpdateSchema(
    string collection,
    [FromBody] SchemaUpdateRequest request,
    [FromServices] ISchemaDiffEngine diffEngine,
    [FromServices] IChangeClassifier classifier,
    [FromServices] IDataInspector? inspector,  // Optional
    CancellationToken cancellationToken)
{
    // 1. Validate request
    if (request.ClientId <= 0) return BadRequest(...);
    
    // 2. Get current schema (if exists)
    var currentSchema = await GetCurrentSchemaAsync(request.ClientId, collection);
    
    // 3. Parse proposed schema
    using var proposedDoc = JsonDocument.Parse(request.JsonSchema);
    using var currentDoc = currentSchema != null 
        ? JsonDocument.Parse(currentSchema.JsonSchema) 
        : null;
    
    // 4. === SENTINEL PIPELINE ===
    if (currentDoc != null)  // Only on updates, not creates
    {
        var pipeline = new SentinelPipeline(diffEngine, classifier, inspector);
        var context = new ClassifierContext(
            ExistingTableCount: await GetTableCountAsync(request.ClientId, collection),
            IsSystemCollection: collection.StartsWith("vibe_") || collection.StartsWith("system_"));
        
        var verdict = await pipeline.AnalyzeAsync(currentDoc, proposedDoc, context, cancellationToken);
        
        if (verdict.Blocked)
        {
            var statusCode = verdict.Verdict == SentinelVerdict.Prohibited ? 422 : 409;
            var errorCode = verdict.Verdict == SentinelVerdict.Prohibited 
                ? "SCHEMA_CHANGE_PROHIBITED" 
                : "SCHEMA_CHANGE_DESTRUCTIVE";
            
            return StatusCode(statusCode, new
            {
                success = false,
                error = new { code = errorCode, message = "..." },
                sentinel = new
                {
                    verdict = verdict.Verdict.ToString(),
                    canOverride = verdict.Verdict == SentinelVerdict.Destructive,
                    riskItems = verdict.RiskReport
                }
            });
        }
        
        // Optional: Include verdict in success response for transparency
        _logger.LogInformation("SENTINEL_PASSED: Verdict={Verdict}", verdict.Verdict);
    }
    
    // 5. Proceed with insert/update...
}
```

### 3. Configuration

```json
// appsettings.json
{
  "VibeSQL": {
    "Sentinel": {
      "Enabled": true,
      "BlockDestructive": true,      // Return 409 for D-300 codes
      "BlockProhibited": true,       // Return 422 for P-400 codes
      "InspectorBudgetMs": 500,      // Total inspection time budget
      "PerQueryTimeoutMs": 5000      // Per-query timeout
    }
  }
}
```

---

## Extensibility: The IDataInspector Pattern

The spec defines VS-SS as open-source-core with proprietary extensions. The `IDataInspector` interface enables this:

### For Real PostgreSQL Tables (Open Source)
```csharp
// VibeSQL.Core/Sentinel/PostgresTableInspector.cs
public class PostgresTableInspector : IDataInspector
{
    // Queries information_schema, pg_class
    // Used by: Self-hosted VibeSQL with hard tables
}
```

### For JSONB Document Storage (Proprietary Extension)
```csharp
// In proprietary consumer (e.g., PayEz Vibe API)
public class VibeSqlDocumentInspector : IDataInspector
{
    // Queries vibe.documents JSONB table
    // Counts: data->>'field' IS NOT NULL
}
```

### Registration Pattern
```csharp
// Consumer overrides default inspector
services.AddVibeSentinelServices();
services.AddSingleton<IDataInspector, VibeSqlDocumentInspector>();  // Replaces default
```

---

## Testing Strategy

### Unit Tests (VibeSQL.Sentinel.Tests)

```csharp
[Theory]
[InlineData("int", "bigint", SentinelVerdict.Migration)]      // M-204: WidenColumnType
[InlineData("varchar(50)", "varchar(100)", SentinelVerdict.Safe)] // S-103: ExpandTextLength
[InlineData("varchar(100)", "varchar(50)", SentinelVerdict.Destructive)] // D-302
public void TypeChange_ClassifiesCorrectly(string oldType, string newType, SentinelVerdict expected)
{
    // Arrange
    var diff = CreateColumnTypeChangeDiff(oldType, newType);
    var classifier = new ChangeClassifier();
    
    // Act
    var result = classifier.Classify(diff);
    
    // Assert
    result.Verdict.Should().Be(expected);
}
```

### Integration Tests (VibeSQL.Server.Tests)

```csharp
[Fact]
public async Task DropTable_WithData_Returns409()
{
    // Arrange: Create schema, insert documents
    await _client.PutAsJsonAsync("/v1/schemas/test", new { ... });
    await _client.PostAsJsonAsync("/v1/collections/test/tables/items", new { ... });
    
    // Act: Try to update schema without the table
    var response = await _client.PutAsJsonAsync("/v1/schemas/test", newSchemaWithoutTable);
    
    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    var body = await response.Content.ReadFromJsonAsync<SentinelErrorResponse>();
    body.Sentinel.Verdict.Should().Be("Destructive");
    body.Sentinel.RiskItems.Should().Contain(r => r.Code == "D-300");
}

[Fact]
public async Task DropTable_EmptyTable_Returns200()
{
    // Arrange: Create schema, NO documents
    await _client.PutAsJsonAsync("/v1/schemas/test", new { ... });
    // No inserts!
    
    // Act: Try to update schema without the table
    var response = await _client.PutAsJsonAsync("/v1/schemas/test", newSchemaWithoutTable);
    
    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.OK);
    // Inspector downgraded D-300 → Migration because no data
}
```

---

## API Response Contract

### Success (Safe/Migration)
```json
{
  "success": true,
  "data": {
    "collection_schema_id": 123,
    "collection": "orders",
    "version": 2,
    "is_active": true
  },
  "meta": {
    "version": 2,
    "previousVersion": 1,
    "sentinel": {
      "verdict": "Safe",
      "codes": ["S-101", "S-106"]
    }
  }
}
```

### Blocked: Destructive (409)
```json
{
  "success": false,
  "error": {
    "code": "SCHEMA_CHANGE_DESTRUCTIVE",
    "message": "Schema change blocked by Sentinel (Destructive). 2 item(s) flagged."
  },
  "sentinel": {
    "verdict": "Destructive",
    "canOverride": true,
    "riskItems": [
      {
        "code": "D-300",
        "description": "Drop table 'legacy_orders'",
        "tableName": "legacy_orders",
        "columnName": null,
        "rowCount": 1523,
        "dataAtRisk": true,
        "detail": "Table 'legacy_orders' has 1523 row(s)"
      },
      {
        "code": "D-301",
        "description": "Drop column 'temp_field' from 'orders'",
        "tableName": "orders",
        "columnName": "temp_field",
        "rowCount": 8900,
        "dataAtRisk": true,
        "detail": "Column 'temp_field' on 'orders' has 8900 non-null value(s)"
      }
    ]
  }
}
```

### Blocked: Prohibited (422)
```json
{
  "success": false,
  "error": {
    "code": "SCHEMA_CHANGE_PROHIBITED",
    "message": "Schema change blocked by Sentinel (Prohibited). This change cannot be overridden."
  },
  "sentinel": {
    "verdict": "Prohibited",
    "canOverride": false,
    "riskItems": [
      {
        "code": "P-401",
        "description": "Removing 3/5 tables — major regression",
        "tableName": null,
        "rowCount": -1,
        "dataAtRisk": true
      }
    ]
  }
}
```

---

## Migration Path for Existing Consumers

### Phase 1: Add to VibeSQL Server (This Work)
1. Add DI registration extension
2. Integrate into `SchemasController.Put()`
3. Add configuration options
4. Unit + integration tests
5. Update Swagger docs

### Phase 2: Consumers Remove Duplicates
Once VibeSQL Server has VS-SS:

**PayEz Vibe API can remove:**
- `RunSentinelPipeline()` method
- `VibeSqlDocumentInspector` class (or move to server as extension)
- Duplicate diff/classify logic

**PayEz Vibe API keeps:**
- Business rule validation (reserved names)
- Schema locking
- TypeScript generation
- Index synchronization

---

## Files to Review

| File | Purpose | Lines |
|------|---------|-------|
| `VibeSQL.Sentinel/SentinelTaxonomy.cs` | All taxonomy codes | 80 |
| `VibeSQL.Sentinel/ISchemaDiffEngine.cs` | Interfaces & types | 33 |
| `VibeSQL.Sentinel/SchemaDiffEngine.cs` | Diff computation | 120 |
| `VibeSQL.Sentinel/IChangeClassifier.cs` | Classifier interface | 28 |
| `VibeSQL.Sentinel/ChangeClassifier.cs` | Classification rules | 244 |
| `VibeSQL.Sentinel/IDataInspector.cs` | Inspector interface | 34 |
| `VibeSQL.Sentinel/SentinelPipeline.cs` | Orchestration | 150 |
| `VibeSQL.Core/Sentinel/PostgresTableInspector.cs` | PG table queries | 212 |
| `VibeSQL.Server/Controllers/SchemasController.cs` | **Integration target** | 226 |
| `VibeSQL.Server/Program.cs` | **DI registration target** | 156 |

---

## Open Questions for Review

1. **Should Sentinel run on schema CREATE (not just UPDATE)?**
   - Current spec: Only on updates
   - Alternative: Run always for consistency

2. **Override mechanism for Destructive changes?**
   - Spec says 409 with `canOverride: true`
   - How is override triggered? Header? Query param? Separate endpoint?

3. **Inspector fallback when no IDataInspector registered?**
   - Option A: Assume worst case (all destructive changes blocked)
   - Option B: Allow through (opt-in protection)
   - Option C: Require inspector for destructive change detection

4. **Multiple inspector support?**
   - If both PostgresTableInspector AND custom inspector registered
   - Composite inspector that runs all? First match?

5. **Async schema migration (Level 2: Migration)?**
   - M-200 codes indicate auto-apply WITH DDL
   - Should this be synchronous or background job?
   - How to report progress?

---

## References

- **VS-SS Spec:** `E:/Repos/Agents/BAPert/specs/VIBESQL-SCHEMA-SENTINEL-SPEC.md`
- **Related:** F-1 (sentinel-fallback-fix.md)
