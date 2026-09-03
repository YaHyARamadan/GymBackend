using FluentValidation;
using GymSaaS.Application.Common.Interfaces;
using GymSaaS.Domain.Entities;
using GymSaaS.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymSaaS.Application.Features.Players.Commands;

public record CreateFacilityPlayerCommand(
    int FacilityId, string Name, string Email, string? Phone,
    DateTime? DateOfBirth, int BranchId) : IRequest<bool>;

public record UpdateFacilityPlayerCommand(
    int FacilityId, int PlayerId, string Name, string Email, string? Phone,
    DateTime? DateOfBirth, int BranchId, bool IsActive) : IRequest<bool>;

public record AssignPlayerSubscriptionCommand(
    int FacilityId, int PlayerId, string PlanName, decimal Price,
    int DurationInDays, DateTime? StartDate) : IRequest<bool>;

public class CreateFacilityPlayerCommandValidator : AbstractValidator<CreateFacilityPlayerCommand>
{
    public CreateFacilityPlayerCommandValidator()
    {
        RuleFor(x => x.FacilityId).GreaterThan(0);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.BranchId).GreaterThan(0);
    }
}

public class UpdateFacilityPlayerCommandValidator : AbstractValidator<UpdateFacilityPlayerCommand>
{
    public UpdateFacilityPlayerCommandValidator()
    {
        RuleFor(x => x.FacilityId).GreaterThan(0);
        RuleFor(x => x.PlayerId).GreaterThan(0);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.BranchId).GreaterThan(0);
    }
}

public class AssignPlayerSubscriptionCommandValidator : AbstractValidator<AssignPlayerSubscriptionCommand>
{
    public AssignPlayerSubscriptionCommandValidator()
    {
        RuleFor(x => x.FacilityId).GreaterThan(0);
        RuleFor(x => x.PlayerId).GreaterThan(0);
        RuleFor(x => x.PlanName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Price).GreaterThanOrEqualTo(0);
        RuleFor(x => x.DurationInDays).GreaterThan(0);
    }
}

public class CreateFacilityPlayerCommandHandler :
    IRequestHandler<CreateFacilityPlayerCommand, bool>
{
    private readonly DbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    public CreateFacilityPlayerCommandHandler(DbContext dbContext, ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
    }

    public async Task<bool> Handle(CreateFacilityPlayerCommand request, CancellationToken cancellationToken)
    {
        EnsureSupervisor();
        await EnsureFacility(request.FacilityId, cancellationToken);
        await EnsureBranch(request.FacilityId, request.BranchId, cancellationToken);

        var email = request.Email.Trim().ToLowerInvariant();
        if (await _dbContext.Set<Player>().IgnoreQueryFilters()
            .AnyAsync(p => p.FacilityId == request.FacilityId && p.Email == email, cancellationToken))
            throw new ConflictException("A player with this email already exists in this facility.");

        _dbContext.Set<Player>().Add(new Player {
            FacilityId = request.FacilityId, BranchId = request.BranchId,
            Name = request.Name.Trim(), Email = email, Phone = request.Phone?.Trim(),
            DateOfBirth = request.DateOfBirth, IsActive = true
        });
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task EnsureFacility(int id, CancellationToken ct)
    {
        if (!await _dbContext.Set<Facility>().IgnoreQueryFilters().AnyAsync(f => f.Id == id, ct))
            throw new NotFoundException("Facility", id);
    }

    private async Task EnsureBranch(int facilityId, int branchId, CancellationToken ct)
    {
        if (!await _dbContext.Set<Branch>().IgnoreQueryFilters()
            .AnyAsync(b => b.Id == branchId && b.FacilityId == facilityId, ct))
            throw new NotFoundException("Branch", branchId);
    }

    private void EnsureSupervisor()
    {
        if (!_currentUserService.IsSupervisor)
            throw new ForbiddenAccessException("Only the supervisor can manage facility players.");
    }
}

public class UpdateFacilityPlayerCommandHandler :
    IRequestHandler<UpdateFacilityPlayerCommand, bool>
{
    private readonly DbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    public UpdateFacilityPlayerCommandHandler(DbContext dbContext, ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
    }

    public async Task<bool> Handle(UpdateFacilityPlayerCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUserService.IsSupervisor)
            throw new ForbiddenAccessException("Only the supervisor can manage facility players.");

        if (!await _dbContext.Set<Branch>().IgnoreQueryFilters()
            .AnyAsync(b => b.Id == request.BranchId && b.FacilityId == request.FacilityId, cancellationToken))
            throw new NotFoundException("Branch", request.BranchId);

        var player = await _dbContext.Set<Player>().IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Id == request.PlayerId && p.FacilityId == request.FacilityId, cancellationToken);
        if (player is null)
            throw new NotFoundException("Player", request.PlayerId);

        var email = request.Email.Trim().ToLowerInvariant();
        if (await _dbContext.Set<Player>().IgnoreQueryFilters()
            .AnyAsync(p => p.FacilityId == request.FacilityId && p.Id != request.PlayerId && p.Email == email, cancellationToken))
            throw new ConflictException("A player with this email already exists in this facility.");

        player.Name = request.Name.Trim();
        player.Email = email;
        player.Phone = request.Phone?.Trim();
        player.DateOfBirth = request.DateOfBirth;
        player.BranchId = request.BranchId;
        player.IsActive = request.IsActive;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}

public class AssignPlayerSubscriptionCommandHandler :
    IRequestHandler<AssignPlayerSubscriptionCommand, bool>
{
    private readonly DbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    public AssignPlayerSubscriptionCommandHandler(DbContext dbContext, ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
    }

    public async Task<bool> Handle(AssignPlayerSubscriptionCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUserService.IsSupervisor)
            throw new ForbiddenAccessException("Only the supervisor can assign subscriptions.");

        var player = await _dbContext.Set<Player>().IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Id == request.PlayerId && p.FacilityId == request.FacilityId, cancellationToken);
        if (player is null)
            throw new NotFoundException("Player", request.PlayerId);

        var start = request.StartDate ?? DateTime.UtcNow;
        var subscription = new Subscription {
            FacilityId = request.FacilityId, PlanName = request.PlanName.Trim(),
            Price = request.Price, DurationInDays = request.DurationInDays,
            StartDate = start, EndDate = start.AddDays(request.DurationInDays)
        };
        _dbContext.Set<Subscription>().Add(subscription);
        await _dbContext.SaveChangesAsync(cancellationToken);

        player.SubscriptionId = subscription.Id;
        player.SubscriptionStartDate = subscription.StartDate;
        player.SubscriptionEndDate = subscription.EndDate;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}
