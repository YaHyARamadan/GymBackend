using FluentValidation;
using GymSaaS.Application.Common.Interfaces;
using GymSaaS.Domain.Entities;
using GymSaaS.Domain.Enums;
using GymSaaS.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymSaaS.Application.Features.Support.Commands;

public record AddSupportTicketMessageCommand(int TicketId, string Message) : IRequest<bool>;
public record CloseSupportTicketCommand(int TicketId) : IRequest<bool>;

public class AddSupportTicketMessageCommandValidator : AbstractValidator<AddSupportTicketMessageCommand>
{
    public AddSupportTicketMessageCommandValidator()
    {
        RuleFor(x => x.TicketId).GreaterThan(0);
        RuleFor(x => x.Message).NotEmpty().MaximumLength(4000);
    }
}

public class AddSupportTicketMessageCommandHandler :
    IRequestHandler<AddSupportTicketMessageCommand, bool>
{
    private readonly DbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    public AddSupportTicketMessageCommandHandler(DbContext dbContext, ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
    }

    public async Task<bool> Handle(AddSupportTicketMessageCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUserService.UserId.HasValue())
            throw new ForbiddenAccessException("An authenticated user is required.");

        var ticket = await _dbContext.Set<SupportTicket>().IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == request.TicketId, cancellationToken);
        if (ticket is null)
            throw new NotFoundException("SupportTicket", request.TicketId);
        if (ticket.Status == "Closed")
            throw new ConflictException("Closed tickets cannot receive new messages.");

        if (_currentUserService.ActorType == ActorType.Owner)
        {
            if (!int.TryParse(_currentUserService.UserId, out var ownerId) ||
                ticket.OwnerId != ownerId)
                throw new ForbiddenAccessException("You can only reply to your own tickets.");
        }
        else if (_currentUserService.ActorType != ActorType.Supervisor)
        {
            throw new ForbiddenAccessException("Only the owner or supervisor can reply to tickets.");
        }

        _dbContext.Set<SupportTicketMessage>().Add(new SupportTicketMessage {
            SupportTicketId = ticket.Id,
            SenderId = _currentUserService.UserId!,
            SenderActorType = _currentUserService.ActorType!.Value,
            Message = request.Message.Trim(),
            SentAt = DateTime.UtcNow
        });
        ticket.Status = _currentUserService.ActorType == ActorType.Supervisor ? "InProgress" : "Open";
        await _dbContext.SaveChangesAsync(cancellationToken);

        if (_currentUserService.ActorType == ActorType.Supervisor)
        {
            _dbContext.Set<Notification>().Add(new Notification {
                RecipientId = ticket.OwnerId.ToString(),
                RecipientActorType = ActorType.Owner,
                FacilityId = ticket.FacilityId,
                Title = "Support ticket updated",
                Message = $"The supervisor replied to ticket #{ticket.Id}."
            });
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return true;
    }
}

public class CloseSupportTicketCommandHandler : IRequestHandler<CloseSupportTicketCommand, bool>
{
    private readonly DbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    public CloseSupportTicketCommandHandler(DbContext dbContext, ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
    }

    public async Task<bool> Handle(CloseSupportTicketCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUserService.IsSupervisor)
            throw new ForbiddenAccessException("Only the supervisor can close support tickets.");

        var ticket = await _dbContext.Set<SupportTicket>().IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == request.TicketId, cancellationToken);
        if (ticket is null)
            throw new NotFoundException("SupportTicket", request.TicketId);

        ticket.Status = "Closed";
        ticket.ClosedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}

internal static class StringExtensions
{
    public static bool HasValue(this string? value) => !string.IsNullOrWhiteSpace(value);
}
