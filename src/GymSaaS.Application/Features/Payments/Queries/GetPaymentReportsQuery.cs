using GymSaaS.Application.Common.Interfaces;
using GymSaaS.Domain.Entities;
using GymSaaS.Domain.Enums;
using GymSaaS.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymSaaS.Application.Features.Payments.Queries;

public record GetPaymentRecordsQuery(
    DateTime? From = null, DateTime? To = null, int? FacilityId = null,
    PaymentType? PaymentType = null, int PageNumber = 1, int PageSize = 50) :
    IRequest<PaymentRecordPageDto>;

public record GetPaymentReportQuery(
    DateTime? From = null, DateTime? To = null, int? FacilityId = null) :
    IRequest<PaymentReportDto>;

public record PaymentRecordReadDto(
    int Id, int FacilityId, string FacilityName, decimal Amount,
    PaymentType PaymentType, string? AddOnFeatureName, DateTime RecordedAt, string? Notes);

public record PaymentRecordPageDto(
    IReadOnlyList<PaymentRecordReadDto> Items, int TotalCount, decimal FilteredTotal);

public record PaymentReportLineDto(
    int FacilityId, string FacilityName, PaymentType PaymentType,
    int PaymentCount, decimal TotalAmount);

public record PaymentReportDto(
    DateTime? From, DateTime? To, IReadOnlyList<PaymentReportLineDto> Lines,
    decimal TotalAmount);

public class GetPaymentRecordsQueryHandler :
    IRequestHandler<GetPaymentRecordsQuery, PaymentRecordPageDto>
{
    private readonly DbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    public GetPaymentRecordsQueryHandler(DbContext dbContext, ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
    }

    public async Task<PaymentRecordPageDto> Handle(
        GetPaymentRecordsQuery request, CancellationToken cancellationToken)
    {
        EnsureSupervisor();

        var query = _dbContext.Set<PaymentRecord>().IgnoreQueryFilters()
            .Include(p => p.Facility).Include(p => p.AddOnFeature).AsNoTracking();

        if (request.From.HasValue) query = query.Where(p => p.RecordedAt >= request.From.Value);
        if (request.To.HasValue) query = query.Where(p => p.RecordedAt < request.To.Value.AddDays(1));
        if (request.FacilityId.HasValue) query = query.Where(p => p.FacilityId == request.FacilityId.Value);
        if (request.PaymentType.HasValue) query = query.Where(p => p.PaymentType == request.PaymentType.Value);

        var total = await query.CountAsync(cancellationToken);
        var amount = await query.SumAsync(p => (decimal?)p.Amount, cancellationToken) ?? 0m;
        var size = Math.Clamp(request.PageSize, 1, 200);
        var page = Math.Max(0, request.PageNumber - 1);

        var items = await query.OrderByDescending(p => p.RecordedAt)
            .Skip(page * size).Take(size)
            .Select(p => new PaymentRecordReadDto(
                p.Id, p.FacilityId, p.Facility.Name, p.Amount, p.PaymentType,
                p.AddOnFeature != null ? p.AddOnFeature.Name : null, p.RecordedAt, p.Notes))
            .ToListAsync(cancellationToken);

        return new PaymentRecordPageDto(items, total, amount);
    }

    private void EnsureSupervisor()
    {
        if (!_currentUserService.IsSupervisor)
            throw new ForbiddenAccessException("Only the supervisor can read payment records.");
    }
}

public class GetPaymentReportQueryHandler :
    IRequestHandler<GetPaymentReportQuery, PaymentReportDto>
{
    private readonly DbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    public GetPaymentReportQueryHandler(DbContext dbContext, ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
    }

    public async Task<PaymentReportDto> Handle(
        GetPaymentReportQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUserService.IsSupervisor)
            throw new ForbiddenAccessException("Only the supervisor can read payment reports.");

        var query = _dbContext.Set<PaymentRecord>().IgnoreQueryFilters()
            .Include(p => p.Facility).AsNoTracking();
        if (request.From.HasValue) query = query.Where(p => p.RecordedAt >= request.From.Value);
        if (request.To.HasValue) query = query.Where(p => p.RecordedAt < request.To.Value.AddDays(1));
        if (request.FacilityId.HasValue) query = query.Where(p => p.FacilityId == request.FacilityId.Value);

        var lines = await query.GroupBy(p => new { p.FacilityId, FacilityName = p.Facility.Name, p.PaymentType })
            .Select(g => new PaymentReportLineDto(
                g.Key.FacilityId, g.Key.FacilityName, g.Key.PaymentType,
                g.Count(), g.Sum(p => p.Amount)))
            .OrderByDescending(x => x.TotalAmount)
            .ToListAsync(cancellationToken);

        return new PaymentReportDto(request.From, request.To, lines, lines.Sum(x => x.TotalAmount));
    }
}
