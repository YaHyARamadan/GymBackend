namespace GymSaaS.Domain.Exceptions;

/// <summary>Thrown when an authenticated user lacks permission → HTTP 403</summary>
public class ForbiddenAccessException : DomainException
{
    public ForbiddenAccessException()
        : base("ليس لديك صلاحية للوصول إلى هذا المورد.") { }

    public ForbiddenAccessException(string message) : base(message) { }
}
