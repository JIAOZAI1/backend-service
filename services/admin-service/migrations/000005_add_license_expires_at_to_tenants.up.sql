ALTER TABLE tenants
    ADD COLUMN license_expires_at DATETIME(6) NULL AFTER db_password;

ALTER TABLE tenants
    ADD KEY idx_tenants_license_expires_at (license_expires_at);
