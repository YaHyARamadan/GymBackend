namespace GymSaaS.Domain.Exceptions;

/// <summary>Thrown when a facility is locked/frozen → HTTP 423</summary>
public class FacilityLockedException : DomainException
{
    public FacilityLockedException()
        : base("المنشأة مجمّدة حاليًا. يرجى التواصل مع الإدارة لتجديد الاشتراك.") { }

    public FacilityLockedException(string message) : base(message) { }
}
