namespace VibeSQL.Core.Models.Escalation;

/// <summary>
/// Request to resume after an escalation.
/// </summary>
public class ResumeRequest
{
    /// <summary>
    /// Notes from the human about the resolution.
    /// </summary>
    public string? ResolutionNotes { get; set; }

    /// <summary>
    /// Optional: Adjust sensitivity level after resume.
    /// </summary>
    public int? AdjustSensitivity { get; set; }

    /// <summary>
    /// Who resolved the escalation.
    /// </summary>
    public string? ResolvedBy { get; set; }
}
