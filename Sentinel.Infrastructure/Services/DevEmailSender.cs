using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace Sentinel.Infrastructure.Services;

/// <summary>
/// Development email sender that writes emails to the log. Replace with real implementation for production.
/// </summary>
public class DevEmailSender : IEmailSender
{
    private readonly ILogger<DevEmailSender> _logger;

    public DevEmailSender(ILogger<DevEmailSender> logger)
    {
        _logger = logger;
    }

    public Task SendAsync(string to, string subject, string body)
    {
        _logger.LogInformation("DEV EMAIL -> To: {To}, Subject: {Subject}, Body: {Body}", to, subject, body);
        return Task.CompletedTask;
    }
}
