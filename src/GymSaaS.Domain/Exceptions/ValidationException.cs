namespace GymSaaS.Domain.Exceptions;

/// <summary>Thrown when input validation fails → HTTP 400</summary>
public class ValidationException : DomainException
{
    public IDictionary<string, string[]> Errors { get; }

    public ValidationException(IDictionary<string, string[]> errors)
        : base("حدث خطأ في التحقق من البيانات المدخلة.")
    {
        Errors = errors;
    }

    public ValidationException(string field, string message)
        : base(message)
    {
        Errors = new Dictionary<string, string[]> { { field, [message] } };
    }
}
