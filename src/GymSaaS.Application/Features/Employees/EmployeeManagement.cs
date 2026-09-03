using FluentValidation;
using GymSaaS.Application.Common.Interfaces;
using GymSaaS.Domain.Entities;
using GymSaaS.Domain.Enums;
using GymSaaS.Domain.Exceptions;
using ValidationException = GymSaaS.Domain.Exceptions.ValidationException;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymSaaS.Application.Features.Employees;

public record CreateEmployeeCommand(
    ActorType Role, int FacilityId, string Name, string Email, string Password,
    string? Phone, int? BranchId, IReadOnlyList<int>? BranchIds, string? Specialization) : IRequest<EmployeeReadDto>;

public record EmployeeReadDto(
    int Id, ActorType Role, int FacilityId, string Name, string Email, string? Phone,
    int? BranchId, IReadOnlyList<int> BranchIds, string? Specialization, bool IsActive, DateTime CreatedAt);

public record GetEmployeesQuery(int? FacilityId = null) : IRequest<IReadOnlyList<EmployeeReadDto>>;

public class CreateEmployeeCommandValidator : AbstractValidator<CreateEmployeeCommand>
{
    public CreateEmployeeCommandValidator()
    {
        RuleFor(x => x.Role).Must(role => role is ActorType.BranchManager or ActorType.Coach or ActorType.Receptionist);
        RuleFor(x => x.FacilityId).GreaterThan(0);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8);
        RuleFor(x => x.BranchId).GreaterThan(0)
            .When(x => x.Role is ActorType.Coach or ActorType.Receptionist);
    }
}

public class CreateEmployeeCommandHandler : IRequestHandler<CreateEmployeeCommand, EmployeeReadDto>
{
    private readonly DbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    public CreateEmployeeCommandHandler(DbContext dbContext, ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
    }

    public async Task<EmployeeReadDto> Handle(CreateEmployeeCommand request, CancellationToken cancellationToken)
    {
        EnsureSupervisor();

        if (!await _dbContext.Set<Facility>().IgnoreQueryFilters()
            .AnyAsync(f => f.Id == request.FacilityId, cancellationToken))
            throw new NotFoundException("Facility", request.FacilityId);

        var email = request.Email.Trim().ToLowerInvariant();
        var duplicate =
            await _dbContext.Set<Owner>().IgnoreQueryFilters().AnyAsync(x => x.Email == email, cancellationToken) ||
            await _dbContext.Set<BranchManager>().IgnoreQueryFilters().AnyAsync(x => x.Email == email, cancellationToken) ||
            await _dbContext.Set<Coach>().IgnoreQueryFilters().AnyAsync(x => x.Email == email, cancellationToken) ||
            await _dbContext.Set<Receptionist>().IgnoreQueryFilters().AnyAsync(x => x.Email == email, cancellationToken) ||
            await _dbContext.Set<Supervisor>().AnyAsync(x => x.Email == email, cancellationToken);
        if (duplicate)
            throw new ConflictException("An account with this email already exists.");

        var branchIds = request.BranchIds?.Distinct().ToArray() ?? [];
        if (request.Role == ActorType.BranchManager)
        {
            if (branchIds.Length == 0 && request.BranchId.HasValue)
                branchIds = [request.BranchId.Value];
            if (branchIds.Length == 0)
                throw new ValidationException("BranchIds", "A branch manager needs at least one assigned branch.");
        }
        else
        {
            if (!request.BranchId.HasValue)
                throw new ValidationException("BranchId", "This role requires a branch.");
            branchIds = [request.BranchId.Value];
        }

        var validBranchIds = await _dbContext.Set<Branch>().IgnoreQueryFilters()
            .Where(b => b.FacilityId == request.FacilityId && branchIds.Contains(b.Id))
            .Select(b => b.Id).ToListAsync(cancellationToken);
        if (validBranchIds.Count != branchIds.Length)
            throw new NotFoundException("Branch", branchIds[0]);

        var hash = BCrypt.Net.BCrypt.HashPassword(request.Password);
        if (request.Role == ActorType.BranchManager)
        {
            var e = new BranchManager {
                FacilityId = request.FacilityId, Name = request.Name.Trim(), Email = email,
                PasswordHash = hash, Phone = request.Phone?.Trim(),
                AssignedBranchIds = string.Join(",", branchIds), IsActive = true
            };
            _dbContext.Set<BranchManager>().Add(e);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return new EmployeeReadDto(e.Id, request.Role, e.FacilityId, e.Name, e.Email, e.Phone,
                null, branchIds, null, e.IsActive, e.CreatedAt);
        }

        if (request.Role == ActorType.Coach)
        {
            var e = new Coach {
                FacilityId = request.FacilityId, BranchId = request.BranchId!.Value,
                Name = request.Name.Trim(), Email = email, PasswordHash = hash,
                Phone = request.Phone?.Trim(), Specialization = request.Specialization?.Trim(), IsActive = true
            };
            _dbContext.Set<Coach>().Add(e);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return new EmployeeReadDto(e.Id, request.Role, e.FacilityId, e.Name, e.Email, e.Phone,
                e.BranchId, branchIds, e.Specialization, e.IsActive, e.CreatedAt);
        }

        var receptionist = new Receptionist {
            FacilityId = request.FacilityId, BranchId = request.BranchId!.Value,
            Name = request.Name.Trim(), Email = email, PasswordHash = hash,
            Phone = request.Phone?.Trim(), IsActive = true
        };
        _dbContext.Set<Receptionist>().Add(receptionist);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return new EmployeeReadDto(receptionist.Id, request.Role, receptionist.FacilityId, receptionist.Name,
            receptionist.Email, receptionist.Phone, receptionist.BranchId, branchIds, null,
            receptionist.IsActive, receptionist.CreatedAt);
    }

    private void EnsureSupervisor()
    {
        if (!_currentUserService.IsSupervisor)
            throw new ForbiddenAccessException("Only the supervisor can manage employees.");
    }
}

public class GetEmployeesQueryHandler : IRequestHandler<GetEmployeesQuery, IReadOnlyList<EmployeeReadDto>>
{
    private readonly DbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    public GetEmployeesQueryHandler(DbContext dbContext, ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
    }

    public async Task<IReadOnlyList<EmployeeReadDto>> Handle(
        GetEmployeesQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUserService.IsSupervisor)
            throw new ForbiddenAccessException("Only the supervisor can list employees.");

        var managers = await _dbContext.Set<BranchManager>().IgnoreQueryFilters()
            .Where(e => !request.FacilityId.HasValue || e.FacilityId == request.FacilityId.Value)
            .AsNoTracking().ToListAsync(cancellationToken);
        var coaches = await _dbContext.Set<Coach>().IgnoreQueryFilters()
            .Where(e => !request.FacilityId.HasValue || e.FacilityId == request.FacilityId.Value)
            .AsNoTracking().ToListAsync(cancellationToken);
        var receptionists = await _dbContext.Set<Receptionist>().IgnoreQueryFilters()
            .Where(e => !request.FacilityId.HasValue || e.FacilityId == request.FacilityId.Value)
            .AsNoTracking().ToListAsync(cancellationToken);

        return managers.Select(e => new EmployeeReadDto(e.Id, ActorType.BranchManager, e.FacilityId,
                e.Name, e.Email, e.Phone, null, ParseIds(e.AssignedBranchIds), null, e.IsActive, e.CreatedAt))
            .Concat(coaches.Select(e => new EmployeeReadDto(e.Id, ActorType.Coach, e.FacilityId,
                e.Name, e.Email, e.Phone, e.BranchId, [e.BranchId], e.Specialization, e.IsActive, e.CreatedAt)))
            .Concat(receptionists.Select(e => new EmployeeReadDto(e.Id, ActorType.Receptionist, e.FacilityId,
                e.Name, e.Email, e.Phone, e.BranchId, [e.BranchId], null, e.IsActive, e.CreatedAt)))
            .OrderBy(e => e.Name).ToList();
    }

    private static IReadOnlyList<int> ParseIds(string value) =>
        value.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(x => int.TryParse(x, out var id) ? id : 0)
            .Where(id => id > 0).ToArray();
}
