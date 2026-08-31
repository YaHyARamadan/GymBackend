using FluentValidation;
using GymSaaS.Application.Common.Interfaces;
using GymSaaS.Domain.Entities;
using GymSaaS.Domain.Enums;
using GymSaaS.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymSaaS.Application.Features.Auth.Commands;

public record ImpersonateCommand(int FacilityId, ActorType TargetRole, int? BranchId) : IRequest<ImpersonationTokenResponseDto>;

public record ImpersonationTokenResponseDto(string Token, DateTime ExpiresAt);

public class ImpersonateCommandValidator : AbstractValidator<ImpersonateCommand>
{
    public ImpersonateCommandValidator()
    {
        RuleFor(x => x.FacilityId).GreaterThan(0);
        RuleFor(x => x.TargetRole).IsInEnum();
    }
}

public class ImpersonateCommandHandler : IRequestHandler<ImpersonateCommand, ImpersonationTokenResponseDto>
{
    private readonly DbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IImpersonationTokenService _impersonationTokenService;

    public ImpersonateCommandHandler(DbContext dbContext, ICurrentUserService currentUserService, IImpersonationTokenService impersonationTokenService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _impersonationTokenService = impersonationTokenService;
    }

    public async Task<ImpersonationTokenResponseDto> Handle(ImpersonateCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUserService.IsSupervisor)
            throw new ForbiddenAccessException("فقط السوبرفايزر يمكنه تبديل الأنشطة والأدوار.");

        var facility = await _dbContext.Set<Facility>()
            .FirstOrDefaultAsync(f => f.Id == request.FacilityId, cancellationToken);

        if (facility == null)
            throw new NotFoundException("Facility", request.FacilityId);

        // backend.md §3.5: Sold license hides supervisor endpoints (returns 404 instead of 403)
        if (facility.LicenseType == LicenseType.Sold)
            throw new NotFoundException("Facility", request.FacilityId);

        var ttl = TimeSpan.FromMinutes(45);
        var token = _impersonationTokenService.GenerateImpersonationToken(
            _currentUserService.UserId!,
            request.FacilityId,
            request.TargetRole,
            request.BranchId,
            ttl);

        return new ImpersonationTokenResponseDto(token, DateTime.UtcNow.Add(ttl));
    }
}
