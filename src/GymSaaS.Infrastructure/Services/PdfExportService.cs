using System.Net;
using System.Text;
using GymSaaS.Application.Common.Interfaces;

namespace GymSaaS.Infrastructure.Services;

public class PdfExportService : IPdfExportService
{
    public byte[] GenerateContractPdf(string htmlContent, string ownerName, string signatureText, string ipAddress, DateTime signedAt)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html><html><head><meta charset='UTF-8'>");
        sb.AppendLine("<link href='https://fonts.googleapis.com/css2?family=Aref+Ruqaa&family=Dancing+Script&display=swap' rel='stylesheet'>");
        sb.AppendLine("<style>");
        sb.AppendLine("body { font-family: sans-serif; direction: rtl; padding: 20px; }");
        sb.AppendLine(".signature { font-family: 'Aref Ruqaa', 'Dancing Script', cursive; font-size: 28px; color: #1e3a8a; margin-top: 20px; border-bottom: 2px solid #1e3a8a; display: inline-block; padding-bottom: 5px; }");
        sb.AppendLine(".meta { margin-top: 30px; font-size: 12px; color: #6b7280; border-top: 1px solid #e5e7eb; padding-top: 10px; }");
        sb.AppendLine("</style></head><body>");
        sb.AppendLine(htmlContent);
        sb.AppendLine("<hr/>");
        sb.AppendLine($"<p><strong>توقيع الأونر:</strong></p>");
        sb.AppendLine($"<div class='signature'>{WebUtility.HtmlEncode(signatureText)}</div>");
        sb.AppendLine($"<div class='meta'><p>اسم الموقع: {WebUtility.HtmlEncode(ownerName)}</p><p>عنوان IP: {WebUtility.HtmlEncode(ipAddress)}</p><p>تاريخ ووقت التوقيع: {signedAt:yyyy-MM-dd HH:mm:ss} UTC</p></div>");
        sb.AppendLine("</body></html>");

        string fullHtml = sb.ToString();
        return Encoding.UTF8.GetBytes(fullHtml);
    }
}
