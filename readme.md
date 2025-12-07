# Sentinel

*A central OAuth2/OpenID Connect provider so you stop copy-pasting auth into every app.*

Sentinel is a **modular, reusable authentication and authorization microservice** built with **ASP.NET Core**. It’s designed to act as a **central OAuth2 / OpenID Connect provider** for web/mobile apps, internal tools, and services.

---

## Current Status (dev)
- Auth code + PKCE, refresh tokens, and client credentials wired via OpenIddict.
- Domain clients are synced to OpenIddict applications (create/update/delete together).
- Admin UI/API for OpenIddict clients (`/admin/clients`, `/api/openiddict/clients`).
- User/tenant/scope CRUD APIs; tenant registration page; login/consent pages.
- Admin flag (`IsAdmin`) emits admin role/scope on login for protected admin areas.
- Health checks for Postgres/Redis.

Not production-ready yet: no rate limiting/CAPTCHA, no email verification/reset, and no audit/logging/rotation.

---

## Quickstart (dev)
1) Requirements: .NET SDK 10, Postgres, Redis.
2) Apply migrations:  
   `dotnet ef database update --project Sentinel.Infrastructure --startup-project Sentinel.csproj`
3) Run: `dotnet run` (or `dotnet watch run` for hot reload).
4) Create an admin user (set `IsAdmin=true` in the Users table) and log in via `/account/login`.
5) Register a client via `/admin/clients` (public+PKCE for SPAs; confidential+secret for backends).

### Key endpoints
- Login: `/account/login`
- Register (tenant-slugged): `/t/{slug}/account/register`
- Consent/authorize: `/connect/authorize`
- Token: `/connect/token`
- Userinfo: `/connect/userinfo`
- Health: `/health`
- Admin UI: `/admin/clients` (admin-only)
- Admin APIs: `/api/openiddict/clients`, `/api/users`, `/api/tenants`, `/api/scopes`, `/api/clients`

### Flows
- Auth code + PKCE: `/connect/authorize` → code → `/connect/token` (with code_verifier) → access/ID/refresh tokens.
- Client credentials: `/connect/token` with `grant_type=client_credentials` (confidential clients with secrets; or public if allowed).

---

## Architecture
- `Sentinel.Domain` — Core entities and rules (User, Tenant, Client, Scope, etc.)
- `Sentinel.Application` — Use cases/services (issuing tokens, user management, etc.)
- `Sentinel.Infrastructure` — EF Core/Postgres, Redis, OpenIddict stores, security helpers
- `Sentinel.Api` — ASP.NET Core surface: OpenIddict endpoints, admin APIs, Razor UI

---

## Use Cases
- Central auth for microservices, web/mobile apps, and internal tools.
- Multi-tenant auth with per-tenant clients/scopes.
- Admin UI to onboard clients without code changes.

---

## Remaining Work Before Prod
- Enforce HTTPS/HSTS; add rate limiting/abuse protection.
- Persist signing/encryption keys; add rotation.
- Email verification, password reset, optional MFA.
- Harden consent/login UX and error handling.
- Audit logging, structured metrics, and alerts.
- Lock down admin issuance, remove any dev seeds, and clean malformed OpenIddict app data.
- Rate limiting added (auth/token endpoints); tune values per environment.

### Certificates (signing/encryption keys)
- Set `Auth:SigningCertificate:Path/Password` and `Auth:EncryptionCertificate:Path/Password` (PFX/PKCS12). In production, the app will refuse to start without them.
- Dev fallback uses ephemeral keys only when no cert paths are provided.
- Rotation plan: add new certs, allow overlap, then remove old keys after token TTLs expire.
