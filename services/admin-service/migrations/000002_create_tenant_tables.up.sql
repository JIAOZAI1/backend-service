CREATE TABLE IF NOT EXISTS tenants (
    id            BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    tenant_id     VARCHAR(36)     NOT NULL,
    tenant_code   VARCHAR(32)     NOT NULL,
    db_type       VARCHAR(32)     NOT NULL,
    db_host       VARCHAR(255)    NOT NULL,
    db_port       INT             NOT NULL,
    db_name       VARCHAR(64)     NOT NULL,
    db_username   VARCHAR(32)     NOT NULL,
    db_password   VARCHAR(255)    NOT NULL,
    reviewed_by   BIGINT UNSIGNED NOT NULL,
    status        VARCHAR(16)     NOT NULL DEFAULT 'created',
    created_at    DATETIME(6)     NOT NULL,
    updated_at    DATETIME(6)     NOT NULL,
    deleted_at    DATETIME(6)     NULL,
    PRIMARY KEY (id),
    UNIQUE KEY uk_tenants_tenant_id (tenant_id),
    UNIQUE KEY uk_tenants_tenant_code (tenant_code),
    KEY idx_tenants_deleted_at (deleted_at),
    KEY idx_tenants_status (status)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS user_tenants (
    id         BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    user_id    BIGINT UNSIGNED NOT NULL,
    tenant_id  BIGINT UNSIGNED NOT NULL,
    created_at DATETIME(6)     NOT NULL,
    deleted_at DATETIME(6)     NULL,
    PRIMARY KEY (id),
    UNIQUE KEY uk_user_tenants_user_tenant (user_id, tenant_id),
    KEY idx_user_tenants_deleted_at (deleted_at),
    CONSTRAINT fk_user_tenants_tenant_id FOREIGN KEY (tenant_id) REFERENCES tenants (id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
