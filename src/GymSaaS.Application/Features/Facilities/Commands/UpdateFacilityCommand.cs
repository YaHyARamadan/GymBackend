using FluentValidation;
using GymSaaS.Application.Common.Interfaces;
using GymSaaS.Domain.Entities;
using GymSaaS.Domain.Enums;
using GymSaaS.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymSaaS.Application.Features.Facilities.Commands;

public record UpdateFacilityCommand(
    int FacilityId,
    string Name,
    string? Description,
    LicenseType LicenseType,
    DateTime? LicenseEndDate) : IRequest<bool>;

public class UpdateFacilityCommandValidator : AbstractValidator<UpdateFacilityCommand>
{
    public UpdateFacilityCommandValidator()
    {
        RuleFor(x => x.FacilityId).GreaterThan(0);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LicenseType).IsInEnum();
        RuleFor(x => x.LicenseEndDate)
            .Must((command, date) => command.LicenseType == LicenseType.Sold || date.HasValue)
            .WithMessage("A subscription license requires an end date.");
    }
}

public class UpdateFacilityCommandHandler : IRequestHandler<UpdateFacilityCommand, bool>
{
    private readonly DbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    public UpdateFacilityCommandHandler(DbContext dbContext, ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
    }

    public async Task<bool> Handle(UpdateFacilityCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUserService.IsSupervisor)
            throw new ForbiddenAccessException("Only the supervisor can update facilities.");

        var facility = await _dbContext.Set<Facility>()
            .FirstOrDefaultAsync(f => f.Id == request.FacilityId, cancellationToken);
        if (facility is null)
            throw new NotFoundException("Facility", request.FacilityId);

        facility.Name = request.Name.Trim();
        facility.Description = request.Description?.Trim();
        facility.LicenseType = request.LicenseType;
        facility.LicenseEndDate = request.LicenseType == LicenseType.Sold ? null : request.LicenseEndDate;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}
