using GymSaaS.Application.Common.Interfaces;
using GymSaaS.Domain.Entities;
using GymSaaS.Domain.Enums;
using GymSaaS.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymSaaS.Application.Features.AddOns.Queries;

public record GetAddOnsQuery : IRequest<IReadOnlyList<AddOnReadDto>>;
public record GetFacilityAddOnsQuery(int FacilityId) : IRequest<IReadOnlyList<FacilityAddOnReadDto>>;

public record AddOnReadDto(
    int Id,
    string Name,
    string? Description,
    decimal Price,
    bool IsActiveForSale,
    DateTime CreatedAt
);

public record FacilityAddOnReadDto(
    int Id,
    int FacilityId,
    int AddOnFeatureId,
    string Name,
    string? Description,
    decimal Price,
    AddOnFeatureStatus Status,
    DateTime ActivatedAt,
    DateTime? ExpiresAt
);

public class GetAddOnsQueryHandler : IRequestHandler<GetAddOnsQuery, IReadOnlyList<AddOnReadDto>>
{
    private readonly DbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    public GetAddOnsQueryHandler(DbContext dbContext, ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
    }

    public async Task<IReadOnlyList<AddOnReadDto>> Handle(
        GetAddOnsQuery request,
        CancellationToken cancellationToken)
    {
        EnsureSupervisor();

        return await _dbContext.Set<AddOnFeature>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .OrderBy(a => a.Name)
            .Select(a => new AddOnReadDto(
                a.Id,
                a.Name,
                a.Description,
                a.Price,
                a.IsActiveForSale,
                a.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    private void EnsureSupervisor()
    {
        if (!_currentUserService.IsSupervisor)
            throw new ForbiddenAccessException("Only the supervisor can access add-ons.");
    }
}

public class GetFacilityAddOnsQueryHandler : IRequestHandler<GetFacilityAddOnsQuery, IReadOnlyList<FacilityAddOnReadDto>>
{
    private readonly DbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    public GetFacilityAddOnsQueryHandler(DbContext dbContext, ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
    }

    public async Task<IReadOnlyList<FacilityAddOnReadDto>> Handle(
        GetFacilityAddOnsQuery request,
        CancellationToken cancellationToken)
    {
        if (!_currentUserService.IsSupervisor)
            throw new ForbiddenAccessException("Only the supervisor can access facility add-ons.");

        var facility = await _dbContext.Set<Facility>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == request.FacilityId, cancellationToken);

        if (facility is null || facility.LicenseType == LicenseType.Sold)
            throw new NotFoundException("Facility", request.FacilityId);

        return await _dbContext.Set<FacilityAddOnSubscription>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(s => s.FacilityId == request.FacilityId)
            .OrderBy(s => s.AddOnFeature.Name)
            .Select(s => new FacilityAddOnReadDto(
                s.Id,
                s.FacilityId,
                s.AddOnFeatureId,
                s.AddOnFeature.Name,
                s.AddOnFeature.Description,
                s.AddOnFeature.Price,
                s.Status,
                s.ActivatedAt,
                s.ExpiresAt))
            .ToListAsync(cancellationToken);
    }
}
