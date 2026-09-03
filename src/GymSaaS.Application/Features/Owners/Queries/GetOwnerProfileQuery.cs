using GymSaaS.Application.Common.Interfaces;
using GymSaaS.Domain.Entities;
using GymSaaS.Domain.Enums;
using GymSaaS.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymSaaS.Application.Features.Owners.Queries;

public record GetOwnerProfileQuery : IRequest<OwnerProfileDto>;

public record OwnerProfileDto(
    int Id,
    string Name,
    string Email,
    string? Phone,
    int FacilityId,
    bool ContractSigned,
    bool OnboardingCompleted,
    string FacilityName,
    LicenseType LicenseType,
    FacilityStatus FacilityStatus,
    DateTime? LicenseEndDate
);

public class GetOwnerProfileQueryHandler : IRequestHandler<GetOwnerProfileQuery, OwnerProfileDto>
{
    private readonly DbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    public GetOwnerProfileQueryHandler(DbContext dbContext, ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
    }

    public async Task<OwnerProfileDto> Handle(
        GetOwnerProfileQuery request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.ActorType != ActorType.Owner ||
            !int.TryParse(_currentUserService.UserId, out var ownerId))
        {
            throw new ForbiddenAccessException("Only an owner session can access this profile.");
        }

        var owner = await _dbContext.Set<Owner>()
            .AsNoTracking()
            .Include(o => o.Facility)
            .FirstOrDefaultAsync(o => o.Id == ownerId, cancellationToken);

        if (owner is null)
            throw new NotFoundException("Owner", ownerId);

        return new OwnerProfileDto(
            owner.Id,
            owner.Name,
            owner.Email,
            owner.Phone,
            owner.FacilityId,
            owner.ContractSigned,
            owner.OnboardingCompleted,
            owner.Facility.Name,
            owner.Facility.LicenseType,
            owner.Facility.Status,
            owner.Facility.LicenseEndDate);
    }
}
