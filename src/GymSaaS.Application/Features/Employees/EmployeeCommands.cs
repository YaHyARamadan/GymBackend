using FluentValidation;
using GymSaaS.Application.Common.Interfaces;
using GymSaaS.Domain.Entities;
using GymSaaS.Domain.Enums;
using GymSaaS.Domain.Exceptions;
using ValidationException = GymSaaS.Domain.Exceptions.ValidationException;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymSaaS.Application.Features.Employees;

public record SetEmployeeStatusCommand(ActorType Role, int Id, bool IsActive) : IRequest<bool>;
public record ResetEmployeePasswordCommand(ActorType Role, int Id, string NewPassword) : IRequest<bool>;

public class ResetEmployeePasswordCommandValidator : AbstractValidator<ResetEmployeePasswordCommand>
{
    public ResetEmployeePasswordCommandValidator()
    {
        RuleFor(x => x.Role).Must(role => role is ActorType.BranchManager or ActorType.Coach or ActorType.Receptionist);
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(8);
    }
}

public class SetEmployeeStatusCommandHandler : IRequestHandler<SetEmployeeStatusCommand, bool>
{
    private readonly DbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    public SetEmployeeStatusCommandHandler(DbContext dbContext, ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
    }

    public async Task<bool> Handle(SetEmployeeStatusCommand request, CancellationToken cancellationToken)
    {
        EnsureSupervisor();
        var found = false;

        switch (request.Role)
        {
            case ActorType.BranchManager:
                var manager = await _dbContext.Set<BranchManager>().IgnoreQueryFilters()
                    .FirstOrDefaultAsync(e => e.Id == request.Id, cancellationToken);
                if (manager is not null) { manager.IsActive = request.IsActive; found = true; }
                break;
            case ActorType.Coach:
                var coach = await _dbContext.Set<Coach>().IgnoreQueryFilters()
                    .FirstOrDefaultAsync(e => e.Id == request.Id, cancellationToken);
                if (coach is not null) { coach.IsActive = request.IsActive; found = true; }
                break;
            case ActorType.Receptionist:
                var receptionist = await _dbContext.Set<Receptionist>().IgnoreQueryFilters()
                    .FirstOrDefaultAsync(e => e.Id == request.Id, cancellationToken);
                if (receptionist is not null) { receptionist.IsActive = request.IsActive; found = true; }
                break;
        }

        if (!found)
            throw new NotFoundException("Employee", request.Id);

        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private void EnsureSupervisor()
    {
        if (!_currentUserService.IsSupervisor)
            throw new ForbiddenAccessException("Only the supervisor can manage employees.");
    }
}

public class ResetEmployeePasswordCommandHandler : IRequestHandler<ResetEmployeePasswordCommand, bool>
{
    private readonly DbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    public ResetEmployeePasswordCommandHandler(DbContext dbContext, ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
    }

    public async Task<bool> Handle(ResetEmployeePasswordCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUserService.IsSupervisor)
            throw new ForbiddenAccessException("Only the supervisor can reset employee passwords.");

        var hash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        switch (request.Role)
        {
            case ActorType.BranchManager:
                var manager = await _dbContext.Set<BranchManager>().IgnoreQueryFilters()
                    .FirstOrDefaultAsync(e => e.Id == request.Id, cancellationToken);
                if (manager is null) throw new NotFoundException("Employee", request.Id);
                manager.PasswordHash = hash;
                break;
            case ActorType.Coach:
                var coach = await _dbContext.Set<Coach>().IgnoreQueryFilters()
                    .FirstOrDefaultAsync(e => e.Id == request.Id, cancellationToken);
                if (coach is null) throw new NotFoundException("Employee", request.Id);
                coach.PasswordHash = hash;
                break;
            case ActorType.Receptionist:
                var receptionist = await _dbContext.Set<Receptionist>().IgnoreQueryFilters()
                    .FirstOrDefaultAsync(e => e.Id == request.Id, cancellationToken);
                if (receptionist is null) throw new NotFoundException("Employee", request.Id);
                receptionist.PasswordHash = hash;
                break;
            default:
                throw new ValidationException("Role", "Invalid employee role.");
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}
