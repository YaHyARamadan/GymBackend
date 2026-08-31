using FluentValidation;
using GymSaaS.Application.Common.Interfaces;
using GymSaaS.Domain.Entities;
using GymSaaS.Domain.Exceptions;
using MediatR;

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

        return new SupportTicketDto(ticket.Id, ticket.Subject, ticket.Status, ticket.CreatedAt);
    }
}
