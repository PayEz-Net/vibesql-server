using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace VibeSQL.Sentinel;

/// <summary>
/// Orchestrates the Sentinel pipeline: Validate → Diff → Classify → Inspect → Verdict.
/// Called by schema change endpoints before applying a schema update.
/// </summary>
public class SentinelPipeline
{
    private readonly ISchemaDiffEngine _diffEngine;
    private readonly IChangeClassifier _classifier;
    private readonly IDataInspector? _inspector;
    private readonly ILogger<SentinelPipeline>? _logger;

    public SentinelPipeline(
        ISchemaDiffEngine diffEngine,
        IChangeClassifier classifier,
        IDataInspector? inspector = null,
        ILogger<SentinelPipeline>? logger = null)
    {
        _diffEngine = diffEngine;
        _classifier = classifier;
        _inspector = inspector;
        _logger = logger;
    }

    /// <summary>
    /// Analyze a proposed schema change. Returns the full verdict without applying.
    /// </summary>
    public async Task<SentinelVerdictResult> AnalyzeAsync(
        JsonDocument? currentSchema,
        JsonDocument proposedSchema,
        ClassifierContext? context = null,
        CancellationToken ct = default)
    {
        // Step 1: Diff
        var diff = _diffEngine.ComputeDiff(currentSchema, proposedSchema);

        if (diff.IsEmpty)
        {
            return SentinelVerdictResult.NoChange();
        }

        // Step 2: Classify
        var classification = _classifier.Classify(diff, context);

        _logger?.LogInformation(
            "SENTINEL_CLASSIFIED: Verdict={Verdict}, Items={Count}, RequiresDataCheck={DataCheck}",
            classification.Verdict, classification.Items.Count, classification.RequiresDataCheck);

        // Step 3: Fast path — additive changes skip data inspection
        if (classification.Verdict <= SentinelVerdict.Safe)
        {
            return SentinelVerdictResult.FromClassification(diff, classification);
        }

        // Step 4: Data inspection (if inspector available and items need it)
        DataInspectionResult? inspection = null;
        if (_inspector != null && classification.RequiresDataCheck)
        {
            inspection = await _inspector.InspectAsync(classification.DataCheckItems, ct);

            // Re-evaluate verdict based on inspection
            // If all data checks came back safe (no data at risk), downgrade to Migration
            if (!inspection.HasDataAtRisk && classification.Verdict == SentinelVerdict.Destructive)
            {
                _logger?.LogInformation("SENTINEL_DOWNGRADED: Destructive → Migration (no data at risk)");
                return SentinelVerdictResult.FromInspection(diff, classification, inspection, SentinelVerdict.Migration);
            }
        }

        return SentinelVerdictResult.FromInspection(diff, classification, inspection, classification.Verdict);
    }
}

/// <summary>
/// Complete verdict from the Sentinel pipeline.
/// </summary>
public class SentinelVerdictResult
{
    public SentinelVerdict Verdict { get; init; }
    public bool AutoApply => Verdict <= SentinelVerdict.Migration;
    public bool Blocked => Verdict >= SentinelVerdict.Destructive;
    public SchemaDiff? Diff { get; init; }
    public ClassificationResult? Classification { get; init; }
    public DataInspectionResult? Inspection { get; init; }

    /// <summary>Human-readable risk items for the 409 response body.</summary>
    public List<RiskItem> RiskReport
    {
        get
        {
            var items = new List<RiskItem>();
            if (Classification == null) return items;

            foreach (var item in Classification.Items.Where(i => i.Level >= SentinelVerdict.Destructive))
            {
                var inspected = Inspection?.Items.FirstOrDefault(i => i.Original == item);
                items.Add(new RiskItem
                {
                    Code = item.Code,
                    Description = item.Description,
                    TableName = item.TableName,
                    ColumnName = item.ColumnName,
                    RowCount = inspected?.RowCount ?? -1,
                    DataAtRisk = inspected?.DataAtRisk ?? true,
                    Detail = inspected?.Detail,
                });
            }
            return items;
        }
    }

    public static SentinelVerdictResult NoChange() => new()
    {
        Verdict = SentinelVerdict.NoChange,
    };

    public static SentinelVerdictResult FromClassification(SchemaDiff diff, ClassificationResult classification) => new()
    {
        Verdict = classification.Verdict,
        Diff = diff,
        Classification = classification,
    };

    public static SentinelVerdictResult FromInspection(
        SchemaDiff diff,
        ClassificationResult classification,
        DataInspectionResult? inspection,
        SentinelVerdict resolvedVerdict) => new()
    {
        Verdict = resolvedVerdict,
        Diff = diff,
        Classification = classification,
        Inspection = inspection,
    };
}

public class RiskItem
{
    public string Code { get; init; } = "";
    public string Description { get; init; } = "";
    public string? TableName { get; init; }
    public string? ColumnName { get; init; }
    public long RowCount { get; init; }
    public bool DataAtRisk { get; init; }
    public string? Detail { get; init; }
}
