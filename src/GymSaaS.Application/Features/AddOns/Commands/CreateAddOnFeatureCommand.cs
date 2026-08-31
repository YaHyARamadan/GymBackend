using FluentValidation;
using GymSaaS.Application.Common.Interfaces;
using GymSaaS.Domain.Entities;
using GymSaaS.Domain.Exceptions;
using MediatR;

namespace GymSaaS.Application.Features.AddOns.Commands;

public record CreateAddOnFeatureCommand(string Name, string? Description, decimal Price) : IRequest<AddOnFeatureDto>;

public record AddOnFeatureDto(int Id, string Name, string? Description, decimal Price, bool IsActiveForSale);

public class CreateAddOnFeatureCommandValidator : AbstractValidator<CreateAddOnFeatureCommand>
{
    public CreateAddOnFeatureCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Price).GreaterThanOrEqualTo(0);
    }
}

public class CreateAddOnFeatureCommandHandler : IRequestHandler<CreateAddOnFeatureCommand, AddOnFeatureDto>
{
    private readonly Microsoft.EntityFrameworkCore.DbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    public CreateAddOnFeatureCommandHandler(Microsoft.EntityFrameworkCore.DbContext dbContext, ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
    }

    public async Task<AddOnFeatureDto> Handle(CreateAddOnFeatureCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUserService.IsSupervisor)
            throw new ForbiddenAccessException("فقط السوبرفايزر يمكنه إضافة خطط الأسعار الإضافية.");

        var addOn = new AddOnFeature
        {
            Name = request.Name,
            Description = request.Description,
            Price = request.Price,
            IsActiveForSale = true
        };

        _dbContext.Set<AddOnFeature>().Add(addOn);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new AddOnFeatureDto(addOn.Id, addOn.Name, addOn.Description, addOn.Price, addOn.IsActiveForSale);
    }
}
