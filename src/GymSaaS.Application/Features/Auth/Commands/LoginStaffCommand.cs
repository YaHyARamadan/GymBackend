using FluentValidation;
using GymSaaS.Application.Common.Interfaces;
using GymSaaS.Domain.Entities;
using GymSaaS.Domain.Enums;
using GymSaaS.Domain.Exceptions;
using ValidationException = GymSaaS.Domain.Exceptions.ValidationException;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymSaaS.Application.Features.Auth.Commands;

public record LoginStaffCommand(string Email, string Password) : IRequest<StaffLoginResponseDto>;
public record StaffLoginResponseDto(string Token, ActorType Role, int FacilityId, int? BranchId);

public class LoginStaffCommandValidator : AbstractValidator<LoginStaffCommand>
{
    public LoginStaffCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty();
    }
}

public class LoginStaffCommandHandler : IRequestHandler<LoginStaffCommand, StaffLoginResponseDto>
{
    private static readonly string DummyHash = "$2a$11$e8k8R7wA3VlB3.QvA2Z.2eP2uO8xN9m4K5L6M7N8O9P0Q1R2S3T4U";
    private readonly DbContext _dbContext;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;

    public LoginStaffCommandHandler(DbContext dbContext, IJwtTokenGenerator jwtTokenGenerator)
    {
        _dbContext = dbContext;
        _jwtTokenGenerator = jwtTokenGenerator;
    }

    public async Task<StaffLoginResponseDto> Handle(LoginStaffCommand request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        var manager = await _dbContext.Set<BranchManager>().IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Email == email, cancellationToken);
        if (manager is not null)
        {
            Verify(request.Password, manager.PasswordHash);
            if (!manager.IsActive) throw new ForbiddenAccessException("This account is inactive.");
            return new StaffLoginResponseDto(
                _jwtTokenGenerator.GenerateToken(manager.Id.ToString(), manager.Email,
                    ActorType.BranchManager, manager.FacilityId, null),
                ActorType.BranchManager, manager.FacilityId, null);
        }

        var coach = await _dbContext.Set<Coach>().IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Email == email, cancellationToken);
        if (coach is not null)
        {
            Verify(request.Password, coach.PasswordHash);
            if (!coach.IsActive) throw new ForbiddenAccessException("This account is inactive.");
            return new StaffLoginResponseDto(
                _jwtTokenGenerator.GenerateToken(coach.Id.ToString(), coach.Email,
                    ActorType.Coach, coach.FacilityId, coach.BranchId),
                ActorType.Coach, coach.FacilityId, coach.BranchId);
        }

        var receptionist = await _dbContext.Set<Receptionist>().IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Email == email, cancellationToken);
        if (receptionist is not null)
        {
            Verify(request.Password, receptionist.PasswordHash);
            if (!receptionist.IsActive) throw new ForbiddenAccessException("This account is inactive.");
            return new StaffLoginResponseDto(
                _jwtTokenGenerator.GenerateToken(receptionist.Id.ToString(), receptionist.Email,
                    ActorType.Receptionist, receptionist.FacilityId, receptionist.BranchId),
                ActorType.Receptionist, receptionist.FacilityId, receptionist.BranchId);
        }

        BCrypt.Net.BCrypt.Verify(request.Password, DummyHash);
        throw new ValidationException("Email", "The email or password is invalid.");
    }

    private static void Verify(string password, string hash)
    {
        if (!BCrypt.Net.BCrypt.Verify(password, hash))
            throw new ValidationException("Email", "The email or password is invalid.");
    }
}
