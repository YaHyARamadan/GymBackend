namespace GymSaaS.Domain.Entities;

public class Branch
{
    public int Id { get; set; }

    public string Name { get; set; } = default!;

    public string? Address { get; set; }

    public string? Phone { get; set; }

    public int FacilityId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public bool IsActive { get; set; } = true;

    // Navigation
    public Facility Facility { get; set; } = default!;
    public ICollection<Coach> Coaches { get; set; } = [];
    public ICollection<Receptionist> Receptionists { get; set; } = [];
}
