CREATE TABLE IF NOT EXISTS database_instances (
    id                  BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    name                VARCHAR(128)    NOT NULL,
    db_type             VARCHAR(32)     NOT NULL,
    host                VARCHAR(255)    NOT NULL,
    port                INT             NOT NULL,
    username            VARCHAR(64)     NOT NULL,
    encrypted_password  VARCHAR(512)    NOT NULL,
    created_at          DATETIME(6)     NOT NULL,
    updated_at          DATETIME(6)     NOT NULL,
    deleted_at          DATETIME(6)     NULL,
    PRIMARY KEY (id),
    UNIQUE KEY uk_database_instances_name (name),
    KEY idx_database_instances_deleted_at (deleted_at)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
