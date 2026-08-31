using FluentValidation;
using GymSaaS.Application.Common.Interfaces;
using GymSaaS.Domain.Entities;
using GymSaaS.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymSaaS.Application.Features.Owners.Commands;

public record CompleteOnboardingCommand(string FacilityPhone, string MainBranchName, string? MainBranchAddress) : IRequest<bool>;

public class CompleteOnboardingCommandValidator : AbstractValidator<CompleteOnboardingCommand>
{
    public CompleteOnboardingCommandValidator()
    {
        RuleFor(x => x.MainBranchName).NotEmpty().MaximumLength(100).WithMessage("اسم الفرع الرئيسي مطلوب.");
    }
}

public class CompleteOnboardingCommandHandler : IRequestHandler<CompleteOnboardingCommand, bool>
{
    private readonly DbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    public CompleteOnboardingCommandHandler(DbContext dbContext, ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
    }

    public async Task<bool> Handle(CompleteOnboardingCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUserService.FacilityId.HasValue || string.IsNullOrEmpty(_currentUserService.UserId))
            throw new ForbiddenAccessException("يجب أن تكون مسجل كأونر منشأة لتنفيذ هذا الإجراء.");

        int ownerId = int.Parse(_currentUserService.UserId);
        var owner = await _dbContext.Set<Owner>()
            .FirstOrDefaultAsync(o => o.Id == ownerId, cancellationToken);

        if (owner == null)
            throw new NotFoundException("Owner", ownerId);

        if (!owner.ContractSigned)
            throw new ForbiddenAccessException("يجب التوقيع على العقد الإلكتروني أولاً قبل إكمال التهيئة.");

        var mainBranch = new Branch
        {
            FacilityId = owner.FacilityId,
            Name = request.MainBranchName,
            Address = request.MainBranchAddress,
            Phone = request.FacilityPhone,
            IsActive = true
        };

        _dbContext.Set<Branch>().Add(mainBranch);
        owner.OnboardingCompleted = true;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }
}
