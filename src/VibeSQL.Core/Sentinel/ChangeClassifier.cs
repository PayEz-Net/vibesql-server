namespace VibeSQL.Core.Sentinel;

/// <summary>
/// Rules engine that classifies each item in a SchemaDiff using the Sentinel Taxonomy.
/// Deterministic — same diff always produces same classification.
/// </summary>
public class ChangeClassifier : IChangeClassifier
{
    public ClassificationResult Classify(SchemaDiff diff, ClassifierContext? context = null)
    {
        var ctx = context ?? new ClassifierContext();
        var items = new List<SentinelItem>();

        if (diff.IsEmpty)
        {
            return new ClassificationResult { Verdict = SentinelVerdict.NoChange, Items = items };
        }

        // ── Tables Added ──────────────────────────────────────────
        foreach (var table in diff.TablesAdded)
        {
            items.Add(new SentinelItem(
                SentinelTaxonomy.S100_AddTable,
                SentinelVerdict.Safe,
                $"Add new table '{table}'",
                TableName: table));
        }

        // ── Tables Removed ────────────────────────────────────────
        foreach (var table in diff.TablesRemoved)
        {
            // P-401: removing >50% of tables is Prohibited
            if (ctx.ExistingTableCount > 0 && diff.TablesRemoved.Count > ctx.ExistingTableCount / 2)
            {
                items.Add(new SentinelItem(
                    SentinelTaxonomy.P401_MajorTableRegression,
                    SentinelVerdict.Prohibited,
                    $"Removing {diff.TablesRemoved.Count}/{ctx.ExistingTableCount} tables — major regression",
                    TableName: table));
            }
            else
            {
                // D-300: drop table — requires data check
                items.Add(new SentinelItem(
                    SentinelTaxonomy.D300_DropTable,
                    SentinelVerdict.Destructive,
                    $"Drop table '{table}'",
                    TableName: table,
                    RequiresDataCheck: true));
            }
        }

        // ── System collection guard ───────────────────────────────
        if (ctx.IsSystemCollection && (diff.TablesRemoved.Count > 0 || diff.ColumnsRemoved.Count > 0))
        {
            items.Add(new SentinelItem(
                SentinelTaxonomy.P403_ModifySystemSchema,
                SentinelVerdict.Prohibited,
                "Cannot modify system collection schema"));
        }

        // ── Columns Added ─────────────────────────────────────────
        foreach (var (table, columns) in diff.ColumnsAdded)
        {
            foreach (var col in columns)
            {
                if (col.Nullable)
                {
                    items.Add(new SentinelItem(
                        SentinelTaxonomy.S101_AddNullableColumn,
                        SentinelVerdict.Safe,
                        $"Add nullable column '{col.Name}' to '{table}'",
                        TableName: table, ColumnName: col.Name));
                }
                else if (col.Default != null)
                {
                    items.Add(new SentinelItem(
                        SentinelTaxonomy.M200_AddNonNullWithDefault,
                        SentinelVerdict.Migration,
                        $"Add non-null column '{col.Name}' with default to '{table}'",
                        TableName: table, ColumnName: col.Name));
                }
                else
                {
                    // Non-null without default — destructive if table has data
                    items.Add(new SentinelItem(
                        SentinelTaxonomy.D311_TightenConstraint,
                        SentinelVerdict.Destructive,
                        $"Add non-null column '{col.Name}' without default to '{table}' — existing rows would violate constraint",
                        TableName: table, ColumnName: col.Name,
                        RequiresDataCheck: true));
                }
            }
        }

        // ── Columns Removed ───────────────────────────────────────
        foreach (var (table, columns) in diff.ColumnsRemoved)
        {
            foreach (var col in columns)
            {
                items.Add(new SentinelItem(
                    SentinelTaxonomy.D301_DropColumn,
                    SentinelVerdict.Destructive,
                    $"Drop column '{col.Name}' from '{table}'",
                    TableName: table, ColumnName: col.Name,
                    RequiresDataCheck: true));
            }
        }

        // ── Columns Modified ──────────────────────────────────────
        foreach (var (table, mods) in diff.ColumnsModified)
        {
            foreach (var mod in mods)
            {
                ClassifyColumnModification(items, table, mod);
            }
        }

        // Overall verdict = highest severity
        var verdict = items.Count > 0
            ? items.Max(i => i.Level)
            : SentinelVerdict.NoChange;

        return new ClassificationResult { Verdict = verdict, Items = items };
    }

    private static void ClassifyColumnModification(List<SentinelItem> items, string table, ColumnModification mod)
    {
        // Nullable change
        if (mod.OldDef.Nullable && !mod.NewDef.Nullable)
        {
            // Nullable → non-null: requires data check for existing NULLs
            items.Add(new SentinelItem(
                SentinelTaxonomy.D306_NullableToNonNullHasNulls,
                SentinelVerdict.Destructive,
                $"Column '{mod.ColumnName}' on '{table}': nullable → non-null",
                TableName: table, ColumnName: mod.ColumnName,
                RequiresDataCheck: true));
        }
        else if (!mod.OldDef.Nullable && mod.NewDef.Nullable)
        {
            // Non-null → nullable: safe (loosening)
            items.Add(new SentinelItem(
                SentinelTaxonomy.S104_LoosenConstraint,
                SentinelVerdict.Safe,
                $"Column '{mod.ColumnName}' on '{table}': non-null → nullable (loosened)",
                TableName: table, ColumnName: mod.ColumnName));
        }

        // Type change
        if (!string.Equals(mod.OldDef.Type, mod.NewDef.Type, StringComparison.OrdinalIgnoreCase))
        {
            if (IsWidening(mod.OldDef.Type, mod.NewDef.Type))
            {
                items.Add(new SentinelItem(
                    SentinelTaxonomy.S103_ExpandTextLength,
                    SentinelVerdict.Safe,
                    $"Column '{mod.ColumnName}' on '{table}': widen type {mod.OldDef.Type} → {mod.NewDef.Type}",
                    TableName: table, ColumnName: mod.ColumnName));
            }
            else if (IsSafeUpcast(mod.OldDef.Type, mod.NewDef.Type))
            {
                items.Add(new SentinelItem(
                    SentinelTaxonomy.M204_WidenColumnType,
                    SentinelVerdict.Migration,
                    $"Column '{mod.ColumnName}' on '{table}': upcast {mod.OldDef.Type} → {mod.NewDef.Type}",
                    TableName: table, ColumnName: mod.ColumnName));
            }
            else if (IsNarrowing(mod.OldDef.Type, mod.NewDef.Type))
            {
                items.Add(new SentinelItem(
                    SentinelTaxonomy.D302_NarrowColumnType,
                    SentinelVerdict.Destructive,
                    $"Column '{mod.ColumnName}' on '{table}': narrow type {mod.OldDef.Type} → {mod.NewDef.Type}",
                    TableName: table, ColumnName: mod.ColumnName,
                    RequiresDataCheck: true));
            }
            else
            {
                items.Add(new SentinelItem(
                    SentinelTaxonomy.D303_IncompatibleTypeCast,
                    SentinelVerdict.Destructive,
                    $"Column '{mod.ColumnName}' on '{table}': incompatible type change {mod.OldDef.Type} → {mod.NewDef.Type}",
                    TableName: table, ColumnName: mod.ColumnName,
                    RequiresDataCheck: true));
            }
        }
    }

    /// <summary>Text/varchar widening — always safe.</summary>
    private static bool IsWidening(string oldType, string newType)
    {
        // text is the widest string type — any varchar → text is widening
        if (newType.Equals("text", StringComparison.OrdinalIgnoreCase) &&
            oldType.StartsWith("varchar", StringComparison.OrdinalIgnoreCase))
            return true;

        // varchar(n) → varchar(m) where m > n is widening
        var oldLen = ParseVarcharLength(oldType);
        var newLen = ParseVarcharLength(newType);
        if (oldLen.HasValue && newLen.HasValue && newLen.Value > oldLen.Value)
            return true;

        return false;
    }

    /// <summary>Check if type change is a narrowing (D-302).</summary>
    internal static bool IsNarrowing(string oldType, string newType)
    {
        var oldLen = ParseVarcharLength(oldType);
        var newLen = ParseVarcharLength(newType);
        if (oldLen.HasValue && newLen.HasValue && newLen.Value < oldLen.Value)
            return true;
        // text → varchar(n) is narrowing
        if (oldType.Equals("text", StringComparison.OrdinalIgnoreCase) &&
            newType.StartsWith("varchar", StringComparison.OrdinalIgnoreCase))
            return true;
        return false;
    }

    private static int? ParseVarcharLength(string type)
    {
        var match = System.Text.RegularExpressions.Regex.Match(
            type, @"varchar\((\d+)\)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return match.Success ? int.Parse(match.Groups[1].Value) : null;
    }

    /// <summary>Safe numeric upcasts — int→bigint, real→double, etc.</summary>
    private static bool IsSafeUpcast(string oldType, string newType)
    {
        var old = oldType.ToLowerInvariant();
        var @new = newType.ToLowerInvariant();
        return (old, @new) switch
        {
            ("integer", "bigint") => true,
            ("int", "bigint") => true,
            ("smallint", "integer") => true,
            ("smallint", "bigint") => true,
            ("real", "double precision") => true,
            ("float", "double precision") => true,
            _ => false,
        };
    }
}
