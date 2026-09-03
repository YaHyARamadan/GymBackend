using FluentValidation;
using GymSaaS.Application.Common.Interfaces;
using GymSaaS.Domain.Entities;
using GymSaaS.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymSaaS.Application.Features.Owners.Commands;

public record UpdateOwnerCommand(
    int OwnerId, string Name, string Email, string? Phone,
    bool ContractSigned, bool OnboardingCompleted) : IRequest<bool>;

public record ResetOwnerPasswordCommand(int OwnerId, string NewPassword) : IRequest<bool>;

public class UpdateOwnerCommandValidator : AbstractValidator<UpdateOwnerCommand>
{
    public UpdateOwnerCommandValidator()
    {
        RuleFor(x => x.OwnerId).GreaterThan(0);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
    }
}

public class ResetOwnerPasswordCommandValidator : AbstractValidator<ResetOwnerPasswordCommand>
{
    public ResetOwnerPasswordCommandValidator()
    {
        RuleFor(x => x.OwnerId).GreaterThan(0);
        RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(8);
    }
}

public class UpdateOwnerCommandHandler : IRequestHandler<UpdateOwnerCommand, bool>
{
    private readonly DbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    public UpdateOwnerCommandHandler(DbContext dbContext, ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
    }

    public async Task<bool> Handle(UpdateOwnerCommand request, CancellationToken cancellationToken)
    {
        EnsureSupervisor();
        var owner = await _dbContext.Set<Owner>().IgnoreQueryFilters()
            .FirstOrDefaultAsync(o => o.Id == request.OwnerId, cancellationToken);
        if (owner is null)
            throw new NotFoundException("Owner", request.OwnerId);

        var email = request.Email.Trim().ToLowerInvariant();
        var duplicate =
            await _dbContext.Set<Owner>().IgnoreQueryFilters()
                .AnyAsync(o => o.Id != request.OwnerId && o.Email == email, cancellationToken) ||
            await _dbContext.Set<Supervisor>().AnyAsync(s => s.Email == email, cancellationToken) ||
            await _dbContext.Set<BranchManager>().IgnoreQueryFilters().AnyAsync(e => e.Email == email, cancellationToken) ||
            await _dbContext.Set<Coach>().IgnoreQueryFilters().AnyAsync(e => e.Email == email, cancellationToken) ||
            await _dbContext.Set<Receptionist>().IgnoreQueryFilters().AnyAsync(e => e.Email == email, cancellationToken);
        if (duplicate)
            throw new ConflictException("An account with this email already exists.");

        owner.Name = request.Name.Trim();
        owner.Email = email;
        owner.Phone = request.Phone?.Trim();
        owner.ContractSigned = request.ContractSigned;
        owner.OnboardingCompleted = request.OnboardingCompleted;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private void EnsureSupervisor()
    {
        if (!_currentUserService.IsSupervisor)
            throw new ForbiddenAccessException("Only the supervisor can manage owners.");
    }
}

public class ResetOwnerPasswordCommandHandler : IRequestHandler<ResetOwnerPasswordCommand, bool>
{
    private readonly DbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    public ResetOwnerPasswordCommandHandler(DbContext dbContext, ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
    }

    public async Task<bool> Handle(ResetOwnerPasswordCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUserService.IsSupervisor)
            throw new ForbiddenAccessException("Only the supervisor can reset owner passwords.");

        var owner = await _dbContext.Set<Owner>().IgnoreQueryFilters()
            .FirstOrDefaultAsync(o => o.Id == request.OwnerId, cancellationToken);
        if (owner is null)
            throw new NotFoundException("Owner", request.OwnerId);

        owner.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}
