namespace GymSaaS.Domain.Entities;

/// <summary>
/// Records an owner's acceptance of a specific contract version.
/// Immutable after creation.
/// </summary>
public class ContractApproval
{
    public int Id { get; set; }

    public int ContractId { get; set; }

    public int OwnerId { get; set; }

    public int FacilityId { get; set; }

    public int ContractVersion { get; set; }

    /// <summary>Full name typed by the owner — displayed in handwriting font as signature</summary>
    public string SignatureText { get; set; } = default!;

    public string IpAddress { get; set; } = default!;

    public DateTime SignedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Path/URL to the backup PDF stored separately from this record</summary>
    public string? PdfBackupPath { get; set; }

    // Navigation
    public Contract Contract { get; set; } = default!;
    public Owner Owner { get; set; } = default!;
}
