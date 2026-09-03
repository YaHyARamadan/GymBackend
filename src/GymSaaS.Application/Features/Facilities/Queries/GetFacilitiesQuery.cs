using GymSaaS.Application.Common.Interfaces;
using GymSaaS.Domain.Entities;
using GymSaaS.Domain.Enums;
using GymSaaS.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymSaaS.Application.Features.Facilities.Queries;

public record GetFacilitiesQuery : IRequest<IReadOnlyList<FacilityReadDto>>;
public record GetFacilityQuery(int Id) : IRequest<FacilityReadDto>;

public record FacilityReadDto(
    int Id,
    string Name,
    string? Description,
    LicenseType LicenseType,
    FacilityStatus Status,
    DateTime? LicenseEndDate,
    DateTime CreatedAt,
    int BranchCount,
    string? OwnerEmail
);

public class GetFacilitiesQueryHandler : IRequestHandler<GetFacilitiesQuery, IReadOnlyList<FacilityReadDto>>
{
    private readonly DbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    public GetFacilitiesQueryHandler(DbContext dbContext, ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
    }

    public async Task<IReadOnlyList<FacilityReadDto>> Handle(
        GetFacilitiesQuery request,
        CancellationToken cancellationToken)
    {
        EnsureSupervisor();

        // SQL Server cannot translate ordering by a DTO that contains correlated
        // collection projections. Materialize the supervisor-sized list first,
        // then apply the stable display order in memory.
        var facilities = await ProjectFacilities()
            .ToListAsync(cancellationToken);

        return facilities
            .OrderBy(f => f.Name)
            .ToList();
    }

    private IQueryable<FacilityReadDto> ProjectFacilities()
    {
        return _dbContext.Set<Facility>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Select(f => new FacilityReadDto(
                f.Id,
                f.Name,
                f.Description,
                f.LicenseType,
                f.Status,
                f.LicenseEndDate,
                f.CreatedAt,
                f.Branches.Count(),
                f.Owners.Select(o => o.Email).FirstOrDefault()));
    }

    private void EnsureSupervisor()
    {
        if (!_currentUserService.IsSupervisor)
            throw new ForbiddenAccessException("Only the supervisor can list facilities.");
    }

    public async Task<FacilityReadDto> GetFacility(
        GetFacilityQuery request,
        CancellationToken cancellationToken)
    {
        EnsureSupervisor();

        var facility = await ProjectFacilities()
            .FirstOrDefaultAsync(f => f.Id == request.Id, cancellationToken);

        if (facility is null)
            throw new NotFoundException("Facility", request.Id);

        return facility;
    }
}

public class GetFacilityQueryHandler : IRequestHandler<GetFacilityQuery, FacilityReadDto>
{
    private readonly DbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    public GetFacilityQueryHandler(DbContext dbContext, ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
    }

    public async Task<FacilityReadDto> Handle(
        GetFacilityQuery request,
        CancellationToken cancellationToken)
    {
        if (!_currentUserService.IsSupervisor)
            throw new ForbiddenAccessException("Only the supervisor can access facility details.");

        var facility = await _dbContext.Set<Facility>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(f => f.Id == request.Id)
            .Select(f => new FacilityReadDto(
                f.Id,
                f.Name,
                f.Description,
                f.LicenseType,
                f.Status,
                f.LicenseEndDate,
                f.CreatedAt,
                f.Branches.Count(),
                f.Owners.Select(o => o.Email).FirstOrDefault()))
            .FirstOrDefaultAsync(cancellationToken);

        if (facility is null)
            throw new NotFoundException("Facility", request.Id);

        return facility;
    }
}
