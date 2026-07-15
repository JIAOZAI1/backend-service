ALTER TABLE tenants
    DROP KEY idx_tenants_license_expires_at,
    DROP COLUMN license_expires_at;
