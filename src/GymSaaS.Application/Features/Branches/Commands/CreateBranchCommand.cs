using FluentValidation;
using GymSaaS.Application.Common.Interfaces;
using GymSaaS.Domain.Entities;
using GymSaaS.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymSaaS.Application.Features.Branches.Commands;

public record CreateBranchCommand(string Name, string? Address, string? Phone) : IRequest<BranchDto>;

public record BranchDto(int Id, string Name, string? Address, string? Phone, bool IsActive);

public class CreateBranchCommandValidator : AbstractValidator<CreateBranchCommand>
{
    public CreateBranchCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
    }
}

public class CreateBranchCommandHandler : IRequestHandler<CreateBranchCommand, BranchDto>
{
    private readonly DbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    public CreateBranchCommandHandler(DbContext dbContext, ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
    }

    public async Task<BranchDto> Handle(CreateBranchCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUserService.FacilityId.HasValue)
            throw new ForbiddenAccessException("يجب التواجد داخل منشأة لإنشاء فرع.");

        var branch = new Branch
        {
            FacilityId = _currentUserService.FacilityId.Value,
            Name = request.Name,
            Address = request.Address,
            Phone = request.Phone,
            IsActive = true
        };

        _dbContext.Set<Branch>().Add(branch);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new BranchDto(branch.Id, branch.Name, branch.Address, branch.Phone, branch.IsActive);
    }
}
