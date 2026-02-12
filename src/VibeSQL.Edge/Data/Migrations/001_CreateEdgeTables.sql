-- VibeSQL Edge: Initial schema for OIDC provider management and federated identity
-- Run against the same PostgreSQL database as VibeSQL Server

CREATE SCHEMA IF NOT EXISTS vibe_system;

-- Table 1: OIDC Providers
CREATE TABLE IF NOT EXISTS vibe_system.oidc_providers (
    provider_key            VARCHAR(50) PRIMARY KEY,
    display_name            VARCHAR(200) NOT NULL,
    issuer                  VARCHAR(500) NOT NULL UNIQUE,
    discovery_url           VARCHAR(500) NOT NULL,
    audience                VARCHAR(500) NOT NULL,
    is_active               BOOLEAN DEFAULT TRUE,
    is_bootstrap            BOOLEAN DEFAULT FALSE,
    auto_provision          BOOLEAN DEFAULT FALSE,
    provision_default_role  VARCHAR(100),
    subject_claim_path      VARCHAR(100) DEFAULT 'sub',
    role_claim_path         VARCHAR(100) DEFAULT 'roles',
    email_claim_path        VARCHAR(100) DEFAULT 'email',
    clock_skew_seconds      INTEGER DEFAULT 60,
    disable_grace_minutes   INTEGER DEFAULT 0,
    disabled_at             TIMESTAMPTZ,
    created_at              TIMESTAMPTZ DEFAULT NOW(),
    updated_at              TIMESTAMPTZ DEFAULT NOW()
);

-- Table 2: Role Mappings (external IDP role -> Vibe permission level)
CREATE TABLE IF NOT EXISTS vibe_system.oidc_provider_role_mappings (
    id                  SERIAL PRIMARY KEY,
    provider_key        VARCHAR(50) NOT NULL,
    external_role       VARCHAR(200) NOT NULL,
    vibe_permission     VARCHAR(20) NOT NULL CHECK (vibe_permission IN ('none','read','write','schema','admin')),
    denied_statements   TEXT[],
    allowed_collections TEXT[],
    description         VARCHAR(500),
    created_at          TIMESTAMPTZ DEFAULT NOW(),

    CONSTRAINT uq_provider_role UNIQUE (provider_key, external_role),
    CONSTRAINT fk_role_provider FOREIGN KEY (provider_key)
        REFERENCES vibe_system.oidc_providers(provider_key) ON DELETE CASCADE
);

-- Table 3: Client Mappings (provider + client -> license ceiling)
CREATE TABLE IF NOT EXISTS vibe_system.oidc_provider_client_mappings (
    id              SERIAL PRIMARY KEY,
    provider_key    VARCHAR(50) NOT NULL,
    vibe_client_id  VARCHAR(100) NOT NULL,
    is_active       BOOLEAN DEFAULT TRUE,
    max_permission  VARCHAR(20) DEFAULT 'write' CHECK (max_permission IN ('none','read','write','schema','admin')),
    created_at      TIMESTAMPTZ DEFAULT NOW(),

    CONSTRAINT uq_provider_client UNIQUE (provider_key, vibe_client_id),
    CONSTRAINT fk_client_provider FOREIGN KEY (provider_key)
        REFERENCES vibe_system.oidc_providers(provider_key) ON DELETE CASCADE
);

-- Table 4: Federated Identities (external sub -> internal vibe_user_id)
CREATE TABLE IF NOT EXISTS vibe_system.federated_identities (
    id                  SERIAL PRIMARY KEY,
    provider_key        VARCHAR(50) NOT NULL,
    external_subject    VARCHAR(255) NOT NULL,
    vibe_user_id        INTEGER NOT NULL,
    email               VARCHAR(255),
    display_name        VARCHAR(255),
    first_seen_at       TIMESTAMPTZ DEFAULT NOW(),
    last_seen_at        TIMESTAMPTZ DEFAULT NOW(),
    is_active           BOOLEAN DEFAULT TRUE,
    metadata            JSONB,

    CONSTRAINT uq_federated_identity UNIQUE (provider_key, external_subject),
    CONSTRAINT fk_federated_provider FOREIGN KEY (provider_key)
        REFERENCES vibe_system.oidc_providers(provider_key) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS idx_federated_lookup ON vibe_system.federated_identities (provider_key, external_subject);
CREATE INDEX IF NOT EXISTS idx_federated_vibe_user ON vibe_system.federated_identities (vibe_user_id);

-- Seed: PayEz IDP as bootstrap provider
INSERT INTO vibe_system.oidc_providers (provider_key, display_name, issuer, discovery_url, audience, is_bootstrap, is_active)
VALUES ('payez-idp', 'PayEz Identity Provider', 'https://idp.payez.net',
        'https://idp.payez.net/.well-known/openid-configuration', 'payez-api', TRUE, TRUE)
ON CONFLICT (provider_key) DO NOTHING;

INSERT INTO vibe_system.oidc_provider_role_mappings (provider_key, external_role, vibe_permission, description)
VALUES
    ('payez-idp', 'payez_user', 'write', 'Standard PayEz user - read/write access'),
    ('payez-idp', 'payez_admin', 'admin', 'PayEz admin - full access')
ON CONFLICT (provider_key, external_role) DO NOTHING;
