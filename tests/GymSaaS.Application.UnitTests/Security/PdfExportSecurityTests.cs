using System.Text;
using GymSaaS.Infrastructure.Services;
using Xunit;

namespace GymSaaS.Application.UnitTests.Security;

public class PdfExportSecurityTests
{
    [Fact]
    public void GenerateContractPdf_WithXssPayload_ShouldHtmlEncodeUserInputs()
    {
        // Arrange
        var service = new PdfExportService();
        string maliciousName = "<script>alert('xss')</script>";
        string maliciousSignature = "<img src=x onerror=alert(1)>";
        string maliciousIp = "127.0.0.1<iframe src='bad.site'></iframe>";

        // Act
        byte[] pdfBytes = service.GenerateContractPdf("<p>Contract Content</p>", maliciousName, maliciousSignature, maliciousIp, DateTime.UtcNow);
        string pdfContent = Encoding.UTF8.GetString(pdfBytes);

        // Assert — Script tags and HTML elements must be HTML encoded
        Assert.DoesNotContain("<script>alert('xss')</script>", pdfContent);
        Assert.Contains("&lt;script&gt;alert(&#39;xss&#39;)&lt;/script&gt;", pdfContent);

        Assert.DoesNotContain("<img src=x onerror=alert(1)>", pdfContent);
        Assert.Contains("&lt;img src=x onerror=alert(1)&gt;", pdfContent);
    }
}
