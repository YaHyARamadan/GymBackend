using GymSaaS.Application.Common.Interfaces;
using GymSaaS.Domain.Entities;
using GymSaaS.Domain.Enums;
using GymSaaS.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymSaaS.Application.Features.Facilities.Commands;

public record UnlockFacilityCommand(int FacilityId, decimal AmountPaid, string IdempotencyKey) : IRequest<bool>;

public class UnlockFacilityCommandHandler : IRequestHandler<UnlockFacilityCommand, bool>
{
    private readonly DbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    public UnlockFacilityCommandHandler(DbContext dbContext, ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
    }

    public async Task<bool> Handle(UnlockFacilityCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUserService.IsSupervisor)
            throw new ForbiddenAccessException("فقط السوبرفايزر يمكنه فك قفل المنشأة.");

        var facility = await _dbContext.Set<Facility>()
            .FirstOrDefaultAsync(f => f.Id == request.FacilityId, cancellationToken);

        if (facility == null)
            throw new NotFoundException("Facility", request.FacilityId);

        if (facility.LicenseType == LicenseType.Sold)
            throw new NotFoundException("Facility", request.FacilityId);

        var duplicatePayment = await _dbContext.Set<PaymentRecord>()
            .AnyAsync(p => p.IdempotencyKey == request.IdempotencyKey, cancellationToken);
        if (duplicatePayment)
            return true; // Already processed idempotently

        facility.Status = FacilityStatus.Active;

        var payment = new PaymentRecord
        {
            FacilityId = request.FacilityId,
            Amount = request.AmountPaid,
            PaymentType = PaymentType.PlatformSubscription,
            IdempotencyKey = request.IdempotencyKey,
            RecordedAt = DateTime.UtcNow,
            Notes = "فك قفل وتأكيد اشتراك المنشأة"
        };
        _dbContext.Set<PaymentRecord>().Add(payment);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }
}
