namespace GymSaaS.Domain.Entities;

/// <summary>
/// Versioned contract text. When contract changes, owners must re-approve.
/// Text is stored in DB — not a static string in code.
/// </summary>
public class Contract
{
    public int Id { get; set; }

    public int Version { get; set; }

    /// <summary>HTML content of the contract text</summary>
    public string Content { get; set; } = default!;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public ICollection<ContractApproval> Approvals { get; set; } = [];
}
