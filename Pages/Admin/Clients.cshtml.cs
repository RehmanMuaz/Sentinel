using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

[Authorize(Policy = "ManageClients")]
public class ClientsPageModel : PageModel
{
    private readonly IOpenIddictApplicationManager _manager;

    public ClientsPageModel(IOpenIddictApplicationManager manager)
    {
        _manager = manager;
    }

    [BindProperty]
    public string ClientId { get; set; } = string.Empty;
    [BindProperty]
    public string DisplayName { get; set; } = string.Empty;
    [BindProperty]
    public bool IsPublic { get; set; } = true;
    [BindProperty]
    public string? ClientSecret { get; set; }
    [BindProperty]
    public bool AllowClientCredentials { get; set; } = false;
    [BindProperty]
    public bool RequirePkce { get; set; } = true;
    [BindProperty]
    public string? RedirectUris { get; set; }
    [BindProperty]
    public string? AllowedScopes { get; set; }

    public string? Error { get; set; }
    public string? Success { get; set; }
    public List<ClientSummaryVm> Clients { get; set; } = new();

    public async Task OnGet()
    {
        await LoadClients();
    }

    public async Task<IActionResult> OnPost()
    {
        if (string.IsNullOrWhiteSpace(ClientId) || string.IsNullOrWhiteSpace(DisplayName))
        {
            Error = "ClientId and DisplayName are required.";
            await LoadClients();
            return Page();
        }

        if (!IsPublic && string.IsNullOrWhiteSpace(ClientSecret))
        {
            Error = "Client secret is required for confidential clients.";
            await LoadClients();
            return Page();
        }

        var existing = await _manager.FindByClientIdAsync(ClientId);
        if (existing is not null)
        {
            Error = "ClientId already exists.";
            await LoadClients();
            return Page();
        }

        var descriptor = BuildDescriptor();
        await _manager.CreateAsync(descriptor);
        Success = "Client created.";
        await LoadClients();
        return Page();
    }

    public async Task<IActionResult> OnPostUpdate()
    {
        if (string.IsNullOrWhiteSpace(ClientId) || string.IsNullOrWhiteSpace(DisplayName))
        {
            Error = "ClientId and DisplayName are required.";
            await LoadClients();
            return Page();
        }

        var app = await _manager.FindByClientIdAsync(ClientId);
        if (app is null)
        {
            Error = "Client not found.";
            await LoadClients();
            return Page();
        }

        var descriptor = BuildDescriptor();
        await _manager.UpdateAsync(app, descriptor);
        Success = "Client updated.";
        await LoadClients();
        return Page();
    }

    public async Task<IActionResult> OnPostDelete(string clientId)
    {
        if (string.IsNullOrWhiteSpace(clientId))
        {
            Error = "ClientId is required.";
            await LoadClients();
            return Page();
        }

        var app = await _manager.FindByClientIdAsync(clientId);
        if (app is null)
        {
            Error = "Client not found.";
            await LoadClients();
            return Page();
        }

        await _manager.DeleteAsync(app);
        Success = "Client deleted.";
        await LoadClients();
        return Page();
    }

    private async Task LoadClients()
    {
        Clients.Clear();
        await foreach (var app in _manager.ListAsync())
        {
            Clients.Add(new ClientSummaryVm
            {
                ClientId = await _manager.GetClientIdAsync(app) ?? string.Empty,
                DisplayName = await _manager.GetDisplayNameAsync(app) ?? string.Empty,
                ClientType = await _manager.GetClientTypeAsync(app) ?? string.Empty,
                Permissions = (await _manager.GetPermissionsAsync(app)).ToArray()
            });
        }
    }

    private OpenIddictApplicationDescriptor BuildDescriptor()
    {
        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = ClientId,
            DisplayName = DisplayName,
            ClientType = IsPublic ? ClientTypes.Public : ClientTypes.Confidential
        };

        descriptor.RedirectUris.Clear();
        var redirects = (RedirectUris ?? string.Empty)
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(u => u.Trim())
            .Where(u => !string.IsNullOrWhiteSpace(u));
        foreach (var uri in redirects)
        {
            descriptor.RedirectUris.Add(new Uri(uri));
        }

        var scopes = (AllowedScopes ?? "api openid")
            .Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        descriptor.Permissions.Clear();
        descriptor.Permissions.UnionWith(new[]
        {
            Permissions.Endpoints.Authorization,
            Permissions.Endpoints.Token,
            Permissions.Endpoints.Revocation,
            Permissions.Endpoints.Introspection,
            Permissions.GrantTypes.AuthorizationCode,
            Permissions.GrantTypes.RefreshToken,
            Permissions.ResponseTypes.Code,
            Permissions.Prefixes.Scope + Scopes.OpenId
        });

        if (AllowClientCredentials)
        {
            descriptor.Permissions.Add(Permissions.GrantTypes.ClientCredentials);
        }

        foreach (var scope in scopes)
        {
            descriptor.Permissions.Add(Permissions.Prefixes.Scope + scope);
        }

        descriptor.Requirements.Clear();
        if (RequirePkce)
        {
            descriptor.Requirements.Add(Requirements.Features.ProofKeyForCodeExchange);
        }

        if (!IsPublic && !string.IsNullOrWhiteSpace(ClientSecret))
        {
            descriptor.ClientSecret = ClientSecret;
        }
        else if (IsPublic)
        {
            descriptor.ClientSecret = null;
        }

        return descriptor;
    }

    public class ClientSummaryVm
    {
        public string ClientId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string ClientType { get; set; } = string.Empty;
        public string[] Permissions { get; set; } = Array.Empty<string>();
    }
}
