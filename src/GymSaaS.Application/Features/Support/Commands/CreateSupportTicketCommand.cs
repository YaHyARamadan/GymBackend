using FluentValidation;
using GymSaaS.Application.Common.Interfaces;
using GymSaaS.Domain.Entities;
using GymSaaS.Domain.Enums;
using GymSaaS.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymSaaS.Application.Features.Support.Commands;

public record CreateSupportTicketCommand(string Subject, string InitialMessage) : IRequest<SupportTicketDto>;

public record SupportTicketDto(int Id, string Subject, string Status, DateTime CreatedAt);

public class CreateSupportTicketCommandValidator : AbstractValidator<CreateSupportTicketCommand>
{
    public CreateSupportTicketCommandValidator()
    {
        RuleFor(x => x.Subject).NotEmpty().MaximumLength(150);
        RuleFor(x => x.InitialMessage).NotEmpty();
    }
}

public class CreateSupportTicketCommandHandler : IRequestHandler<CreateSupportTicketCommand, SupportTicketDto>
{
    private readonly Microsoft.EntityFrameworkCore.DbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    public CreateSupportTicketCommandHandler(Microsoft.EntityFrameworkCore.DbContext dbContext, ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
    }

    public async Task<SupportTicketDto> Handle(CreateSupportTicketCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUserService.FacilityId.HasValue || string.IsNullOrEmpty(_currentUserService.UserId))
            throw new ForbiddenAccessException("يجب التواجد كأونر لفتح تيكت دعم.");

        // The error message above already states this is owner-only, but nothing previously
        // enforced it: ownerId was taken straight from the UserId claim and stored as-is, so a
        // Coach/BranchManager/Receptionist could open a ticket that appears to be from whatever
        // Owner.Id happens to numerically match their own unrelated Id (see SignContractCommand /
        // CompleteOnboardingCommand for the same class of issue).
        if (_currentUserService.ActorType != ActorType.Owner)
            throw new ForbiddenAccessException("يجب التواجد كأونر لفتح تيكت دعم.");

        int ownerId = int.Parse(_currentUserService.UserId);

        var ticket = new SupportTicket
        {
            FacilityId = _currentUserService.FacilityId.Value,
            OwnerId = ownerId,
            Subject = request.Subject,
            Status = "Open",
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Set<SupportTicket>().Add(ticket);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var msg = new SupportTicketMessage
        {
            SupportTicketId = ticket.Id,
            SenderId = _currentUserService.UserId,
            SenderActorType = _currentUserService.ActorType ?? Domain.Enums.ActorType.Owner,
            Message = request.InitialMessage,
            SentAt = DateTime.UtcNow
        };

        _dbContext.Set<SupportTicketMessage>().Add(msg);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var supervisorId = await _dbContext.Set<Supervisor>()
            .Select(s => (int?)s.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (supervisorId.HasValue)
        {
            _dbContext.Set<Notification>().Add(new Notification
            {
                RecipientId = supervisorId.Value.ToString(),
                RecipientActorType = ActorType.Supervisor,
                FacilityId = ticket.FacilityId,
                Title = "New support ticket",
                Message = $"A new ticket was opened: {ticket.Subject}"
            });
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return new SupportTicketDto(ticket.Id, ticket.Subject, ticket.Status, ticket.CreatedAt);
    }
}
