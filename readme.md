# Sentinel

Sentinel is a **modular, reusable authentication and authorization microservice** built with **ASP.NET Core**.  
It’s designed to act as a **central OAuth2 / OpenID Connect provider** that you can plug into multiple apps and services (portfolios, internal tools, SaaS products, etc.) without rewriting auth every time.

---

## Key Features

- **Standards-based Auth**
  - OAuth2 + OpenID Connect flows (authorization code + PKCE, client credentials, refresh tokens)
  - JWT access tokens with a public JWKs endpoint for validation
  - Well-known configuration endpoint (`/.well-known/openid-configuration`)

- **Modular & Extensible Design**
  - Clean separation of **Domain**, **Application**, **Infrastructure**, and **API** layers
  - Pluggable user store, token store, and identity providers
  - Easy to add new login methods (local accounts, Google, GitHub, etc.)

- **Multi-Tenant & Multi-Client**
  - First-class support for **tenants** (e.g. apps, future SaaS projects)
  - Configurable **clients** with their own redirect URIs, allowed scopes, and token lifetimes

- **Built for Real-World Use**
  - ASP.NET Core for high performance and long-term support
  - PostgreSQL for persistent data storage
  - Redis-ready for sessions, blacklisting, and rate limiting
  - Container-friendly (Docker) for easy deployment

- **Integration-Friendly**
  - Any backend (FastAPI, Node, Next.js, etc.) can validate tokens via standard JWT + JWKs
  - Frontend apps can use standard OAuth2/OIDC flows with redirect-based login

---

## Planned Features

- Revocation + logout everywhere: short-lived blacklist of token jti/subject pairs in Redis so revoked tokens expire without bloating Postgres.
- Refresh token rotation tracking: track the latest refresh-token family key to reject older tokens quickly with minimal DB hits.
- Rate limiting / abuse protection: sliding-window throttles for login, reset, and token endpoints backed by Redis.
- Ephemeral protocol state: PKCE verifiers, nonces, device codes, and one-time challenges stored with short TTLs in Redis.
- Session cache: cached user/claim snapshots to speed validation while keeping Postgres as source of truth.
- Distributed locks: guard against double consumption of auth codes/refresh tokens under load.
- Admin + self-service consoles: manage tenants, clients, scopes, users, MFA, and consents.
- External IdPs: plug-and-play Google/GitHub/OIDC/SAML with claims mapping and account linking.
- Observability & audit: structured logs, metrics, traces, and audit trails for sign-ins, token events, and admin actions.
- Deployment hardening: health checks, key rotation jobs, backup/restore guidance, and secure defaults (CSP, HSTS).

---

## High-Level Architecture

- `Sentinel.Domain` – Core entities and business rules (User, Tenant, Client, Role, Token, etc.)
- `Sentinel.Application` – Use cases / services (register, login, issue tokens, revoke tokens, etc.)
- `Sentinel.Infrastructure` – Data access (EF Core, PostgreSQL), Redis, email/SMS providers
- `Sentinel.Api` – ASP.NET Core API surface, endpoints, middleware, and configuration

This structure keeps the auth logic **isolated, testable, and reusable** across any number of projects.

---

## Use Cases

- Central auth server for:
  - Microservices
  - Web apps (Next.js, React, etc.)
  - Mobile apps
  - Internal tools & admin dashboards

If you’re tired of copy-pasting login code into every new project, Sentinel is meant to be the one place that handles it all.
