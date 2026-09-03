using GymSaaS.Application.Common.Interfaces;
using GymSaaS.Domain.Entities;
using GymSaaS.Domain.Enums;
using GymSaaS.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymSaaS.Application.Features.Owners.Queries;

public record GetOwnersQuery(int? FacilityId = null) : IRequest<IReadOnlyList<OwnerManagementDto>>;

public record OwnerManagementDto(
    int Id, int FacilityId, string Name, string Email, string? Phone,
    bool ContractSigned, bool OnboardingCompleted, FacilityStatus FacilityStatus, DateTime CreatedAt);

public class GetOwnersQueryHandler : IRequestHandler<GetOwnersQuery, IReadOnlyList<OwnerManagementDto>>
{
    private readonly DbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    public GetOwnersQueryHandler(DbContext dbContext, ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
    }

    public async Task<IReadOnlyList<OwnerManagementDto>> Handle(
        GetOwnersQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUserService.IsSupervisor)
            throw new ForbiddenAccessException("Only the supervisor can list owners.");

        return await _dbContext.Set<Owner>().IgnoreQueryFilters()
            .Include(o => o.Facility)
            .Where(o => !request.FacilityId.HasValue || o.FacilityId == request.FacilityId.Value)
            .AsNoTracking()
            .OrderBy(o => o.Name)
            .Select(o => new OwnerManagementDto(
                o.Id, o.FacilityId, o.Name, o.Email, o.Phone, o.ContractSigned,
                o.OnboardingCompleted, o.Facility.Status, o.CreatedAt))
            .ToListAsync(cancellationToken);
    }
}
