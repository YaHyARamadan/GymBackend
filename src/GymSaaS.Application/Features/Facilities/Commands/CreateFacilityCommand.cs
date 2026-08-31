using FluentValidation;
using GymSaaS.Application.Common.Interfaces;
using GymSaaS.Domain.Entities;
using GymSaaS.Domain.Enums;
using GymSaaS.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymSaaS.Application.Features.Facilities.Commands;

public record CreateFacilityCommand(string Name, string? Description, LicenseType LicenseType, DateTime? LicenseEndDate, string OwnerName, string OwnerEmail, string OwnerPassword) : IRequest<FacilityDto>;

public record FacilityDto(int Id, string Name, LicenseType LicenseType, FacilityStatus Status, DateTime CreatedAt);

public class CreateFacilityCommandValidator : AbstractValidator<CreateFacilityCommand>
{
    public CreateFacilityCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100).WithMessage("اسم المنشأة مطلوب ولا يتجاوز 100 حرف.");
        RuleFor(x => x.LicenseType).IsInEnum();
        RuleFor(x => x.OwnerName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.OwnerEmail).NotEmpty().EmailAddress();
        RuleFor(x => x.OwnerPassword).NotEmpty().MinimumLength(6);
    }
}

public class CreateFacilityCommandHandler : IRequestHandler<CreateFacilityCommand, FacilityDto>
{
    private readonly DbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    public CreateFacilityCommandHandler(DbContext dbContext, ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
    }

    public async Task<FacilityDto> Handle(CreateFacilityCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUserService.IsSupervisor)
            throw new ForbiddenAccessException("فقط السوبرفايزر يمكنه إنشاء منشأة جديدة.");

        var existingOwner = await _dbContext.Set<Owner>()
            .AnyAsync(o => o.Email == request.OwnerEmail, cancellationToken);
        if (existingOwner)
            throw new ConflictException("البريد الإلكتروني للأونر مستخدم بالفعل.");

        var facility = new Facility
        {
            Name = request.Name,
            Description = request.Description,
            LicenseType = request.LicenseType,
            LicenseEndDate = request.LicenseType == LicenseType.Sold ? request.LicenseEndDate : null,
            Status = FacilityStatus.Active
        };

        _dbContext.Set<Facility>().Add(facility);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var owner = new Owner
        {
            Name = request.OwnerName,
            Email = request.OwnerEmail,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.OwnerPassword),
            FacilityId = facility.Id,
            ContractSigned = false,
            OnboardingCompleted = false
        };

        _dbContext.Set<Owner>().Add(owner);

        var platformSub = new PlatformSubscription
        {
            FacilityId = facility.Id,
            Status = FacilityStatus.Active,
            StartDate = DateTime.UtcNow,
            EndDate = request.LicenseEndDate,
            AmountPaid = 0
        };
        _dbContext.Set<PlatformSubscription>().Add(platformSub);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new FacilityDto(facility.Id, facility.Name, facility.LicenseType, facility.Status, facility.CreatedAt);
    }
}
