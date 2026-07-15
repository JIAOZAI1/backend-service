ALTER TABLE tenants
    ADD COLUMN database_instance_id BIGINT UNSIGNED NULL AFTER db_username;

ALTER TABLE tenants
    ADD KEY idx_tenants_database_instance_id (database_instance_id),
    ADD CONSTRAINT fk_tenants_database_instance_id FOREIGN KEY (database_instance_id) REFERENCES database_instances (id);
