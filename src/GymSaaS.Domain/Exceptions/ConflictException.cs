namespace GymSaaS.Domain.Exceptions;

/// <summary>Thrown when a conflict occurs (e.g. duplicate) → HTTP 409</summary>
public class ConflictException : DomainException
{
    public ConflictException(string message) : base(message) { }
}
