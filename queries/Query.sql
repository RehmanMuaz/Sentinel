UPDATE "OpenIddictApplications"
SET "Permissions" = '[
  "ept:authorization",
  "ept:token",
  "ept:revocation",
  "ept:introspection",
  "gt:authorization_code",
  "gt:refresh_token",
  "rst:code",
  "scp:openid",
  "scp:manage:clients",
  "scp:api"
]'
WHERE "ClientId" = 'admin-client';
