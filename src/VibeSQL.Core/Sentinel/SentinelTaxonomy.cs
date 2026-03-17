namespace VibeSQL.Core.Sentinel;

/// <summary>
/// Schema Sentinel classification codes.
/// Every atomic schema change is classified into exactly one code.
/// The overall verdict is the highest-risk code present.
/// </summary>
public static class SentinelTaxonomy
{
    // ── Level 1: SAFE (auto-apply, log) ──────────────────────────

    public const string S100_AddTable = "S-100";
    public const string S101_AddNullableColumn = "S-101";
    public const string S102_AddColumnWithDefault = "S-102";
    public const string S103_ExpandTextLength = "S-103";
    public const string S104_LoosenConstraint = "S-104";
    public const string S105_AddEnumValue = "S-105";
    public const string S106_AddIndex = "S-106";
    public const string S107_AddCheckNotValid = "S-107";
    public const string S108_ChangeComment = "S-108";
    public const string S109_ChangeDefaultNullable = "S-109";

    // ── Level 2: MIGRATION (auto-apply with generated DDL) ───────

    public const string M200_AddNonNullWithDefault = "M-200";
    public const string M201_AddFkConstraint = "M-201";
    public const string M202_AddUniqueNoDupes = "M-202";
    public const string M203_NullableToNonNullNoNulls = "M-203";
    public const string M204_WidenColumnType = "M-204";
    public const string M205_AddTableWithFk = "M-205";

    // ── Level 3: DESTRUCTIVE (block, return diff + data report) ──

    public const string D300_DropTable = "D-300";
    public const string D301_DropColumn = "D-301";
    public const string D302_NarrowColumnType = "D-302";
    public const string D303_IncompatibleTypeCast = "D-303";
    public const string D304_RenameTable = "D-304";
    public const string D305_RenameColumn = "D-305";
    public const string D306_NullableToNonNullHasNulls = "D-306";
    public const string D307_AddUniqueHasDupes = "D-307";
    public const string D308_DropFkConstraint = "D-308";
    public const string D309_DropPkConstraint = "D-309";
    public const string D310_DropCheckConstraint = "D-310";
    public const string D311_TightenConstraint = "D-311";
    public const string D312_FullReplaceRegression = "D-312";

    // ── Level 4: PROHIBITED (never auto-apply, no override) ──────

    public const string P400_DropEntireSchema = "P-400";
    public const string P401_MajorTableRegression = "P-401";
    public const string P402_ChangePkType = "P-402";
    public const string P403_ModifySystemSchema = "P-403";
    public const string P404_DropTableWithFkDependents = "P-404";
}

/// <summary>
/// Verdict levels, ordered by severity.
/// Overall verdict = highest level present in the change set.
/// </summary>
public enum SentinelVerdict
{
    NoChange = 0,
    Safe = 1,
    Migration = 2,
    Destructive = 3,
    Prohibited = 4,
}

/// <summary>
/// A single classified schema change item.
/// </summary>
public record SentinelItem(
    string Code,
    SentinelVerdict Level,
    string Description,
    string? TableName = null,
    string? ColumnName = null,
    bool RequiresDataCheck = false
);
