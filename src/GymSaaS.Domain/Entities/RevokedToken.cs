namespace GymSaaS.Domain.Entities;

public class RevokedToken
{
    public int Id { get; set; }

    public string Jti { get; set; } = default!;

    public DateTime ExpiresAt { get; set; }

    public DateTime RevokedAt { get; set; } = DateTime.UtcNow;
}
