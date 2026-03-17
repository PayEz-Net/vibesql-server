using System.Text.Json;

namespace VibeSQL.Core.Sentinel;

/// <summary>
/// Computes structural diff between old and new JSON schemas.
/// Handles VibeSQL multi-table format: { "tables": { "tableName": { "properties": { ... } } } }
/// </summary>
public class SchemaDiffEngine : ISchemaDiffEngine
{
    public SchemaDiff ComputeDiff(JsonDocument? oldSchema, JsonDocument newSchema)
    {
        var oldTables = oldSchema != null ? ExtractTables(oldSchema) : new Dictionary<string, Dictionary<string, ColumnDef>>();
        var newTables = ExtractTables(newSchema);

        var diff = new SchemaDiff
        {
            TablesAdded = newTables.Keys.Except(oldTables.Keys, StringComparer.OrdinalIgnoreCase).ToList(),
            TablesRemoved = oldTables.Keys.Except(newTables.Keys, StringComparer.OrdinalIgnoreCase).ToList(),
            ColumnsAdded = new Dictionary<string, List<ColumnDef>>(),
            ColumnsRemoved = new Dictionary<string, List<ColumnDef>>(),
            ColumnsModified = new Dictionary<string, List<ColumnModification>>(),
        };

        // Compare columns in tables that exist in both old and new
        var commonTables = oldTables.Keys.Intersect(newTables.Keys, StringComparer.OrdinalIgnoreCase);
        foreach (var table in commonTables)
        {
            var oldCols = oldTables[table];
            var newCols = newTables[table];

            // Find the actual key (case may differ)
            var oldKey = oldTables.Keys.First(k => k.Equals(table, StringComparison.OrdinalIgnoreCase));
            var newKey = newTables.Keys.First(k => k.Equals(table, StringComparison.OrdinalIgnoreCase));

            var added = newCols.Keys.Except(oldCols.Keys, StringComparer.OrdinalIgnoreCase)
                .Select(c => newCols[c]).ToList();
            var removed = oldCols.Keys.Except(newCols.Keys, StringComparer.OrdinalIgnoreCase)
                .Select(c => oldCols[c]).ToList();

            if (added.Count > 0) diff.ColumnsAdded[newKey] = added;
            if (removed.Count > 0) diff.ColumnsRemoved[oldKey] = removed;

            // Check for type/nullable changes on common columns
            var commonCols = oldCols.Keys.Intersect(newCols.Keys, StringComparer.OrdinalIgnoreCase);
            var modifications = new List<ColumnModification>();
            foreach (var col in commonCols)
            {
                var oldCol = oldCols[col];
                var newCol = newCols[col];
                if (!string.Equals(oldCol.Type, newCol.Type, StringComparison.OrdinalIgnoreCase) ||
                    oldCol.Nullable != newCol.Nullable)
                {
                    modifications.Add(new ColumnModification(col, oldKey, oldCol, newCol));
                }
            }
            if (modifications.Count > 0) diff.ColumnsModified[oldKey] = modifications;
        }

        return diff;
    }

    /// <summary>
    /// Extract table definitions from a JSON schema document.
    /// Returns: { tableName: { columnName: ColumnDef } }
    /// </summary>
    private static Dictionary<string, Dictionary<string, ColumnDef>> ExtractTables(JsonDocument doc)
    {
        var tables = new Dictionary<string, Dictionary<string, ColumnDef>>(StringComparer.OrdinalIgnoreCase);
        var root = doc.RootElement;

        if (root.TryGetProperty("tables", out var tablesElement) && tablesElement.ValueKind == JsonValueKind.Object)
        {
            foreach (var table in tablesElement.EnumerateObject())
            {
                tables[table.Name] = ExtractColumns(table.Value);
            }
        }
        else if (root.TryGetProperty("properties", out var props))
        {
            // Legacy single-table format
            tables["_root"] = ExtractColumnsFromProperties(props);
        }

        return tables;
    }

    private static Dictionary<string, ColumnDef> ExtractColumns(JsonElement tableDef)
    {
        if (tableDef.TryGetProperty("properties", out var props))
            return ExtractColumnsFromProperties(props);
        return new Dictionary<string, ColumnDef>(StringComparer.OrdinalIgnoreCase);
    }

    private static Dictionary<string, ColumnDef> ExtractColumnsFromProperties(JsonElement props)
    {
        var columns = new Dictionary<string, ColumnDef>(StringComparer.OrdinalIgnoreCase);
        if (props.ValueKind != JsonValueKind.Object) return columns;

        foreach (var prop in props.EnumerateObject())
        {
            var type = "text"; // default
            var nullable = true;
            string? defaultVal = null;

            if (prop.Value.TryGetProperty("type", out var typeEl))
                type = typeEl.GetString() ?? "text";
            if (prop.Value.TryGetProperty("x-vibe-nullable", out var nullEl))
                nullable = nullEl.GetBoolean();
            else if (prop.Value.TryGetProperty("nullable", out var null2))
                nullable = null2.GetBoolean();
            if (prop.Value.TryGetProperty("default", out var defEl))
                defaultVal = defEl.ToString();

            columns[prop.Name] = new ColumnDef(prop.Name, type, nullable, defaultVal);
        }

        return columns;
    }
}
