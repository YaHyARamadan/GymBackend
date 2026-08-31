using FluentValidation;
using GymSaaS.Application.Common.Interfaces;
using GymSaaS.Domain.Entities;
using GymSaaS.Domain.Enums;
using GymSaaS.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymSaaS.Application.Features.AddOns.Commands;

public record ActivateFacilityAddOnCommand(int FacilityId, int AddOnFeatureId, decimal AmountPaid, string IdempotencyKey) : IRequest<bool>;

public class ActivateFacilityAddOnCommandValidator : AbstractValidator<ActivateFacilityAddOnCommand>
{
    public ActivateFacilityAddOnCommandValidator()
    {
        RuleFor(x => x.FacilityId).GreaterThan(0);
        RuleFor(x => x.AddOnFeatureId).GreaterThan(0);
        RuleFor(x => x.AmountPaid).GreaterThanOrEqualTo(0);
        RuleFor(x => x.IdempotencyKey).NotEmpty();
    }
}

public class ActivateFacilityAddOnCommandHandler : IRequestHandler<ActivateFacilityAddOnCommand, bool>
{
    private readonly DbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    public ActivateFacilityAddOnCommandHandler(DbContext dbContext, ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
    }

    public async Task<bool> Handle(ActivateFacilityAddOnCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUserService.IsSupervisor)
            throw new ForbiddenAccessException("فقط السوبرفايزر يمكنه تفعيل الميزات الإضافية.");

        var facility = await _dbContext.Set<Facility>()
            .FirstOrDefaultAsync(f => f.Id == request.FacilityId, cancellationToken);

        if (facility == null)
            throw new NotFoundException("Facility", request.FacilityId);

        // backend.md §3.4 & §3.5: Sold license facilities do NOT have add-ons and hide endpoints (return 404)
        if (facility.LicenseType == LicenseType.Sold)
            throw new NotFoundException("Facility", request.FacilityId);

        var addOn = await _dbContext.Set<AddOnFeature>()
            .FirstOrDefaultAsync(a => a.Id == request.AddOnFeatureId, cancellationToken);

        if (addOn == null)
            throw new NotFoundException("AddOnFeature", request.AddOnFeatureId);

        var existingSub = await _dbContext.Set<FacilityAddOnSubscription>()
            .FirstOrDefaultAsync(s => s.FacilityId == request.FacilityId && s.AddOnFeatureId == request.AddOnFeatureId, cancellationToken);

        if (existingSub != null)
        {
            existingSub.Status = AddOnFeatureStatus.Active;
            existingSub.ActivatedAt = DateTime.UtcNow;
        }
        else
        {
            var newSub = new FacilityAddOnSubscription
            {
                FacilityId = request.FacilityId,
                AddOnFeatureId = request.AddOnFeatureId,
                Status = AddOnFeatureStatus.Active,
                ActivatedAt = DateTime.UtcNow
            };
            _dbContext.Set<FacilityAddOnSubscription>().Add(newSub);
        }

        // Internal payment record (backend.md §3.4)
        var payment = new PaymentRecord
        {
            FacilityId = request.FacilityId,
            Amount = request.AmountPaid,
            PaymentType = PaymentType.AddOnFeature,
            AddOnFeatureId = request.AddOnFeatureId,
            IdempotencyKey = request.IdempotencyKey,
            RecordedAt = DateTime.UtcNow,
            Notes = $"تفعيل ميزة إضافية: {addOn.Name}"
        };

        _dbContext.Set<PaymentRecord>().Add(payment);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }
}
