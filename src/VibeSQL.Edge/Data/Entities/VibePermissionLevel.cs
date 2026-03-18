namespace VibeSQL.Edge.Data.Entities;

public enum VibePermissionLevel
{
    None = 0,
    Read = 1,
    Write = 2,
    Schema = 3,
    Admin = 4
}

public static class VibePermissionLevelExtensions
{
    private static readonly Dictionary<string, VibePermissionLevel> Map = new(StringComparer.OrdinalIgnoreCase)
    {
        ["none"] = VibePermissionLevel.None,
        ["read"] = VibePermissionLevel.Read,
        ["write"] = VibePermissionLevel.Write,
        ["schema"] = VibePermissionLevel.Schema,
        ["admin"] = VibePermissionLevel.Admin
    };

    public static VibePermissionLevel Parse(string value)
        => Map.TryGetValue(value, out var level) ? level : VibePermissionLevel.None;

    public static string ToDbString(this VibePermissionLevel level)
        => level switch
        {
            VibePermissionLevel.None => "none",
            VibePermissionLevel.Read => "read",
            VibePermissionLevel.Write => "write",
            VibePermissionLevel.Schema => "schema",
            VibePermissionLevel.Admin => "admin",
            _ => "none"
        };
}
