UPDATE "Clients"
SET "_allowedScopes" = '["api","openid"]'
WHERE "ClientId" = 'dev-client';