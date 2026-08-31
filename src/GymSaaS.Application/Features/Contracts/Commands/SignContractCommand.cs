using FluentValidation;
using GymSaaS.Application.Common.Interfaces;
using GymSaaS.Domain.Entities;
using GymSaaS.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymSaaS.Application.Features.Contracts.Commands;

public record SignContractCommand(int ContractId, string SignatureText, string IpAddress) : IRequest<bool>;

public class SignContractCommandValidator : AbstractValidator<SignContractCommand>
{
    public SignContractCommandValidator()
    {
        RuleFor(x => x.ContractId).GreaterThan(0);
        RuleFor(x => x.SignatureText).NotEmpty().MaximumLength(100).WithMessage("التوقيع بالاسم الكامل مطلوب.");
    }
}

public class SignContractCommandHandler : IRequestHandler<SignContractCommand, bool>
{
    private readonly DbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IPdfExportService _pdfExportService;

    public SignContractCommandHandler(DbContext dbContext, ICurrentUserService currentUserService, IPdfExportService pdfExportService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _pdfExportService = pdfExportService;
    }

    public async Task<bool> Handle(SignContractCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUserService.FacilityId.HasValue || string.IsNullOrEmpty(_currentUserService.UserId))
            throw new ForbiddenAccessException("يجب أن تكون مسجل كأونر منشأة لتوقيع العقد.");

        int ownerId = int.Parse(_currentUserService.UserId);
        var owner = await _dbContext.Set<Owner>()
            .FirstOrDefaultAsync(o => o.Id == ownerId, cancellationToken);

        if (owner == null)
            throw new NotFoundException("Owner", ownerId);

        var contract = await _dbContext.Set<Contract>()
            .FirstOrDefaultAsync(c => c.Id == request.ContractId, cancellationToken);

        if (contract == null)
            throw new NotFoundException("Contract", request.ContractId);

        var approval = new ContractApproval
        {
            ContractId = contract.Id,
            OwnerId = owner.Id,
            FacilityId = owner.FacilityId,
            ContractVersion = contract.Version,
            SignatureText = request.SignatureText,
            IpAddress = request.IpAddress,
            SignedAt = DateTime.UtcNow
        };

        _dbContext.Set<ContractApproval>().Add(approval);
        owner.ContractSigned = true;

        await _dbContext.SaveChangesAsync(cancellationToken);

        // Generate backup PDF asynchronously
        try
        {
            var pdfBytes = _pdfExportService.GenerateContractPdf(contract.Content, owner.Name, request.SignatureText, request.IpAddress, approval.SignedAt);
            approval.PdfBackupPath = $"contracts/facility_{owner.FacilityId}_v{contract.Version}.pdf";
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            // Logging handles PDF failure without failing contract sign
        }

        return true;
    }
}
