using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Sentinel.Infrastructure.Services;

/// <summary>
/// Brevo (Sendinblue) HTTP API email sender.
/// </summary>
public class BrevoEmailSender : IEmailSender
{
    private readonly BrevoEmailOptions _options;
    private readonly HttpClient _httpClient;
    private readonly ILogger<BrevoEmailSender> _logger;

    public BrevoEmailSender(IOptions<BrevoEmailOptions> options, HttpClient httpClient, ILogger<BrevoEmailSender> logger)
    {
        _options = options.Value;
        _httpClient = httpClient;
        _logger = logger;

        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("api-key", _options.ApiKey);
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        _httpClient.BaseAddress = new Uri("https://api.brevo.com/v3/");
    }

    public async Task SendAsync(string to, string subject, string body)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            _logger.LogWarning("Brevo API key is not configured; email not sent.");
            return;
        }

        var payload = new
        {
            sender = new { email = _options.From },
            to = new[] { new { email = to } },
            subject,
            textContent = body
        };

        var json = JsonSerializer.Serialize(payload);
        var response = await _httpClient.PostAsync("smtp/email", new StringContent(json, Encoding.UTF8, "application/json"));

        if (!response.IsSuccessStatusCode)
        {
            var resp = await response.Content.ReadAsStringAsync();
            _logger.LogError("Brevo send failed: {Status} {Content}", response.StatusCode, resp);
        }
    }
}

public class BrevoEmailOptions
{
    public string ApiKey { get; set; } = string.Empty;
    public string From { get; set; } = string.Empty;
}
