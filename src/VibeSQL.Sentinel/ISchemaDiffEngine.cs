using System.Text.Json;

namespace VibeSQL.Sentinel;

/// <summary>
/// Computes the structural diff between two JSON schemas.
/// Pure in-memory comparison — no database access.
/// </summary>
public interface ISchemaDiffEngine
{
    SchemaDiff ComputeDiff(JsonDocument? oldSchema, JsonDocument newSchema);
}

public record ColumnDef(string Name, string Type, bool Nullable, string? Default);

public record ColumnModification(string ColumnName, string TableName, ColumnDef OldDef, ColumnDef NewDef);

public class SchemaDiff
{
    public List<string> TablesAdded { get; init; } = new();
    public List<string> TablesRemoved { get; init; } = new();
    public Dictionary<string, List<ColumnDef>> ColumnsAdded { get; init; } = new();
    public Dictionary<string, List<ColumnDef>> ColumnsRemoved { get; init; } = new();
    public Dictionary<string, List<ColumnModification>> ColumnsModified { get; init; } = new();

    public bool IsEmpty =>
        TablesAdded.Count == 0 && TablesRemoved.Count == 0 &&
        ColumnsAdded.Count == 0 && ColumnsRemoved.Count == 0 &&
        ColumnsModified.Count == 0;

    public bool IsAdditiveOnly =>
        TablesRemoved.Count == 0 && ColumnsRemoved.Count == 0 && ColumnsModified.Count == 0;
}
