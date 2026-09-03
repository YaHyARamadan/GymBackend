using GymSaaS.Application.Common.Interfaces;
using GymSaaS.Domain.Entities;
using GymSaaS.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymSaaS.Application.Features.Notifications;

public record GetNotificationsQuery(int PageNumber = 1, int PageSize = 50) :
    IRequest<NotificationPageDto>;
public record MarkNotificationReadCommand(int Id) : IRequest<bool>;
public record MarkAllNotificationsReadCommand : IRequest<bool>;

public record NotificationReadDto(
    int Id, string Title, string Message, int? FacilityId,
    bool IsRead, DateTime CreatedAt);

public record NotificationPageDto(
    IReadOnlyList<NotificationReadDto> Items, int UnreadCount, int TotalCount);

public class GetNotificationsQueryHandler :
    IRequestHandler<GetNotificationsQuery, NotificationPageDto>
{
    private readonly DbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    public GetNotificationsQueryHandler(DbContext dbContext, ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
    }

    public async Task<NotificationPageDto> Handle(
        GetNotificationsQuery request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_currentUserService.UserId) ||
            !_currentUserService.ActorType.HasValue)
            throw new ForbiddenAccessException("An authenticated session is required.");

        var query = _dbContext.Set<Notification>()
            .Where(n => n.RecipientId == _currentUserService.UserId &&
                        n.RecipientActorType == _currentUserService.ActorType.Value)
            .AsNoTracking();

        var total = await query.CountAsync(cancellationToken);
        var unread = await query.CountAsync(n => !n.IsRead, cancellationToken);
        var items = await query.OrderByDescending(n => n.CreatedAt)
            .Skip(Math.Max(0, request.PageNumber - 1) * request.PageSize)
            .Take(Math.Clamp(request.PageSize, 1, 100))
            .Select(n => new NotificationReadDto(
                n.Id, n.Title, n.Message, n.FacilityId, n.IsRead, n.CreatedAt))
            .ToListAsync(cancellationToken);

        return new NotificationPageDto(items, unread, total);
    }
}

public class MarkNotificationReadCommandHandler :
    IRequestHandler<MarkNotificationReadCommand, bool>
{
    private readonly DbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    public MarkNotificationReadCommandHandler(DbContext dbContext, ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
    }

    public async Task<bool> Handle(MarkNotificationReadCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_currentUserService.UserId) ||
            !_currentUserService.ActorType.HasValue)
            throw new ForbiddenAccessException("An authenticated session is required.");

        var notification = await _dbContext.Set<Notification>().FirstOrDefaultAsync(n =>
            n.Id == request.Id &&
            n.RecipientId == _currentUserService.UserId &&
            n.RecipientActorType == _currentUserService.ActorType.Value, cancellationToken);
        if (notification is null)
            throw new NotFoundException("Notification", request.Id);

        notification.IsRead = true;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}

public class MarkAllNotificationsReadCommandHandler :
    IRequestHandler<MarkAllNotificationsReadCommand, bool>
{
    private readonly DbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    public MarkAllNotificationsReadCommandHandler(DbContext dbContext, ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
    }

    public async Task<bool> Handle(
        MarkAllNotificationsReadCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_currentUserService.UserId) ||
            !_currentUserService.ActorType.HasValue)
            throw new ForbiddenAccessException("An authenticated session is required.");

        await _dbContext.Set<Notification>()
            .Where(n => n.RecipientId == _currentUserService.UserId &&
                        n.RecipientActorType == _currentUserService.ActorType.Value &&
                        !n.IsRead)
            .ExecuteUpdateAsync(setters => setters.SetProperty(n => n.IsRead, true), cancellationToken);
        return true;
    }
}
