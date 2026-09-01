using FluentValidation;
using GymSaaS.Application.Common.Interfaces;
using GymSaaS.Domain.Entities;
using GymSaaS.Domain.Enums;
using GymSaaS.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymSaaS.Application.Features.AuditLog.Queries;

public record GetAuditLogsQuery(int PageNumber = 1, int PageSize = 20) : IRequest<PaginatedAuditLogDto>;

public class GetAuditLogsQueryValidator : AbstractValidator<GetAuditLogsQuery>
{
    public GetAuditLogsQueryValidator()
    {
        RuleFor(x => x.PageNumber).GreaterThan(0);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}

public record PaginatedAuditLogDto(List<AuditLogEntryDto> Items, int TotalCount, int PageNumber, int PageSize);

public record AuditLogEntryDto(
    long Id,
    string ActorId,
    ActorType ActorType,
    string? OnBehalfOfRole,
    string ActionType,
    string EntityType,
    string EntityId,
    string? OldValue,
    string? NewValue,
    DateTime Timestamp,
    int? FacilityId,
    int? BranchId,
    string? CorrelationId
);

public class GetAuditLogsQueryHandler : IRequestHandler<GetAuditLogsQuery, PaginatedAuditLogDto>
{
    private readonly DbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    public GetAuditLogsQueryHandler(DbContext dbContext, ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
    }

    public async Task<PaginatedAuditLogDto> Handle(GetAuditLogsQuery request, CancellationToken cancellationToken)
    {
        // Coach and Reception have NO access to audit logs (backend.md §3.2)
        if (_currentUserService.ActorType == ActorType.Coach || _currentUserService.ActorType == ActorType.Receptionist)
            throw new ForbiddenAccessException("ليس لديك صلاحية للوصول إلى سجل الأنشطة.");

        var query = _dbContext.Set<AuditLogEntry>().AsQueryable();

        // Supervisor (global, real session — not impersonating) is the only actor allowed to see
        // audit logs across every tenant. Every other actor type — including Owner, who has no
        // explicit scoping below prior to this fix — must be confined to their own facility, or
        // they can read every other gym's audit trail (PII, price changes, lock/unlock events, etc.)
        // by simply calling this endpoint with a normal Owner token.
        if (_currentUserService.ActorType != ActorType.Supervisor)
        {
            if (!_currentUserService.FacilityId.HasValue)
                throw new ForbiddenAccessException("ليس لديك صلاحية للوصول إلى سجل الأنشطة.");

            query = query.Where(a => a.FacilityId == _currentUserService.FacilityId.Value);

            // BranchManager is additionally scoped down to only their own branch (backend.md §3.2)
            if (_currentUserService.ActorType == ActorType.BranchManager && _currentUserService.BranchId.HasValue)
            {
                query = query.Where(a => a.BranchId == _currentUserService.BranchId.Value);
            }
        }

        int totalCount = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(a => a.Timestamp)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(a => new AuditLogEntryDto(
                a.Id,
                a.ActorId,
                a.ActorType,
                a.OnBehalfOfRole,
                a.ActionType,
                a.EntityType,
                a.EntityId,
                a.OldValue,
                a.NewValue,
                a.Timestamp,
                a.FacilityId,
                a.BranchId,
                a.CorrelationId
            ))
            .ToListAsync(cancellationToken);

        return new PaginatedAuditLogDto(items, totalCount, request.PageNumber, request.PageSize);
    }
}
