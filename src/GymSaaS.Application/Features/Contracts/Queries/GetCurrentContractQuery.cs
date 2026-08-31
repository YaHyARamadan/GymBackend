using GymSaaS.Application.Common.Interfaces;
using GymSaaS.Domain.Entities;
using GymSaaS.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymSaaS.Application.Features.Contracts.Queries;

public record GetCurrentContractQuery : IRequest<ContractDto>;

public record ContractDto(int Id, int Version, string Content, DateTime CreatedAt);

public class GetCurrentContractQueryHandler : IRequestHandler<GetCurrentContractQuery, ContractDto>
{
    private readonly DbContext _dbContext;

    public GetCurrentContractQueryHandler(DbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ContractDto> Handle(GetCurrentContractQuery request, CancellationToken cancellationToken)
    {
        var contract = await _dbContext.Set<Contract>()
            .Where(c => c.IsActive)
            .OrderByDescending(c => c.Version)
            .FirstOrDefaultAsync(cancellationToken);

        if (contract == null)
        {
            // Seed a default contract if none exists
            contract = new Contract
            {
                Version = 1,
                Content = "<h1>عقد استخدام منصة إدارة الجيمات SaaS</h1><p>بموجب هذا العقد يلتزم الطرفان بالشروط والأحكام الخاصة بالخدمة...</p>",
                IsActive = true
            };
            _dbContext.Set<Contract>().Add(contract);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return new ContractDto(contract.Id, contract.Version, contract.Content, contract.CreatedAt);
    }
}
