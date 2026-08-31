namespace GymSaaS.Application.Common.Interfaces;

public interface IPdfExportService
{
    byte[] GenerateContractPdf(string htmlContent, string ownerName, string signatureText, string ipAddress, DateTime signedAt);
}
