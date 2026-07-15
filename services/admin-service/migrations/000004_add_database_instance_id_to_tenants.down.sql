ALTER TABLE tenants
    DROP FOREIGN KEY fk_tenants_database_instance_id,
    DROP KEY idx_tenants_database_instance_id,
    DROP COLUMN database_instance_id;
