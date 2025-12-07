using System.Threading.Tasks;

namespace Sentinel.Infrastructure.Services;

/// <summary>
/// Abstraction for sending emails. Replace with a real provider (SMTP/SendGrid) in production.
/// </summary>
public interface IEmailSender
{
    Task SendAsync(string to, string subject, string body);
}
