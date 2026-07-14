ALTER TABLE users
    ADD COLUMN review_status VARCHAR(16)     NOT NULL DEFAULT 'pending' AFTER status,
    ADD COLUMN reviewed_by   BIGINT UNSIGNED NULL     AFTER review_status;
