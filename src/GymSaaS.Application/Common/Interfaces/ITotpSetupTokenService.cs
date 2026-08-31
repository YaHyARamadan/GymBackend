namespace GymSaaS.Application.Common.Interfaces;

public interface ITotpSetupTokenService
{
    string GenerateSetupToken(int supervisorId, string? pendingSecret, TimeSpan ttl);
    (bool IsValid, int SupervisorId, string? PendingSecret) ValidateSetupToken(string token);
}
