namespace VibeSQL.Sentinel;

/// <summary>
/// Classifies a SchemaDiff using the Sentinel Taxonomy.
/// Pure rules engine — deterministic, no DB access.
/// </summary>
public interface IChangeClassifier
{
    ClassificationResult Classify(SchemaDiff diff, ClassifierContext? context = null);
}

/// <summary>
/// Optional context for classification decisions (e.g., total table count for P-401).
/// </summary>
public record ClassifierContext(
    int ExistingTableCount = 0,
    bool IsSystemCollection = false
);

public class ClassificationResult
{
    public SentinelVerdict Verdict { get; init; }
    public List<SentinelItem> Items { get; init; } = new();
    public bool RequiresDataCheck => Items.Any(i => i.RequiresDataCheck);

    /// <summary>Items that need a DataInspector query before final verdict.</summary>
    public List<SentinelItem> DataCheckItems => Items.Where(i => i.RequiresDataCheck).ToList();
}
