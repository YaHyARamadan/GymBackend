using FluentValidation;
using GymSaaS.Application.Common.Interfaces;
using GymSaaS.Domain.Entities;
using GymSaaS.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymSaaS.Application.Features.Players.Commands;

public record CreatePlayerCommand(string Name, string Email, string? Phone, DateTime? DateOfBirth, int BranchId) : IRequest<PlayerDto>;

public record PlayerDto(int Id, string Name, string Email, string? Phone, int BranchId, bool IsActive);

public class CreatePlayerCommandValidator : AbstractValidator<CreatePlayerCommand>
{
    public CreatePlayerCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.BranchId).GreaterThan(0);
    }
}

public class CreatePlayerCommandHandler : IRequestHandler<CreatePlayerCommand, PlayerDto>
{
    private readonly Microsoft.EntityFrameworkCore.DbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    public CreatePlayerCommandHandler(Microsoft.EntityFrameworkCore.DbContext dbContext, ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
    }

    public async Task<PlayerDto> Handle(CreatePlayerCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUserService.FacilityId.HasValue)
            throw new ForbiddenAccessException("يجب التواجد داخل منشأة لإضافة لاعب.");

        // Cross-Tenant Branch Isolation: Ensure the requested branch belongs to the current user's facility
        var branch = await _dbContext.Set<Branch>()
            .FirstOrDefaultAsync(b => b.Id == request.BranchId && b.FacilityId == _currentUserService.FacilityId.Value, cancellationToken);

        if (branch == null)
            throw new NotFoundException("Branch", request.BranchId);

        var player = new Player
        {
            FacilityId = _currentUserService.FacilityId.Value,
            BranchId = request.BranchId,
            Name = request.Name,
            Email = request.Email,
            Phone = request.Phone,
            DateOfBirth = request.DateOfBirth,
            IsActive = true
        };

        _dbContext.Set<Player>().Add(player);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new PlayerDto(player.Id, player.Name, player.Email, player.Phone, player.BranchId, player.IsActive);
    }
}
