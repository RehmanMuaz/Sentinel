INSERT INTO "OpenIddictApplications" (
    "Id",
    "ApplicationType",
    "ClientId",
    "ClientSecret",
    "ClientType",
    "DisplayName",
    "DisplayNames",
    "Permissions",
    "RedirectUris",
    "PostLogoutRedirectUris",
    "Requirements",
    "Properties",
    "JsonWebKeySet",
    "Settings",
    "ConsentType",
    "ConcurrencyToken"
)
VALUES (
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',          -- app Id
    'service',                                       -- ApplicationType
    'admin-client',                                  -- ClientId
    '100000.0tpkOphvDwLkU7FmCHEq1w==.bOw+VQQabxXE17aO0V9nJRp0p35bWVfqEFhZI28WETk=',             -- ClientSecret (IdentityV3 hash)
    'confidential',                                  -- ClientType
    'Admin Client',                                  -- DisplayName
    '[]',                                            -- DisplayNames
    '["ept:token","gt:client_credentials","scp:manage:clients","scp:api"]', -- Permissions
    '[]',                                            -- RedirectUris
    '[]',                                            -- PostLogoutRedirectUris
    '[]',                                            -- Requirements
    '[]',                                            -- Properties
    NULL,                                            -- JsonWebKeySet
    '[]',                                            -- Settings
    NULL,                                            -- ConsentType (optional)
    gen_random_uuid()                                -- ConcurrencyToken (or use a GUID)
);
