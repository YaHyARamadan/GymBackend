namespace GymSaaS.Domain.Exceptions;

/// <summary>Thrown when a requested resource is not found → HTTP 404</summary>
public class NotFoundException : DomainException
{
    public NotFoundException(string resourceName, object key)
        : base($"لم يتم العثور على '{resourceName}' بالمعرّف '{key}'.") { }

    public NotFoundException(string message) : base(message) { }
}
