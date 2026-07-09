CREATE TABLE IF NOT EXISTS jobs (
    id              BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    name            VARCHAR(128)    NOT NULL,
    description     VARCHAR(512)    NOT NULL,
    schedule_type   INT             NOT NULL,
    cron_expression VARCHAR(128)    NULL,
    run_at          DATETIME(6)     NULL,
    status          INT             NOT NULL,
    next_run_at     DATETIME(6)     NULL,
    created_at      DATETIME(6)     NOT NULL,
    updated_at      DATETIME(6)     NOT NULL,
    deleted_at      DATETIME(6)     NULL,
    PRIMARY KEY (id),
    KEY idx_jobs_deleted_at (deleted_at),
    KEY idx_jobs_next_run_at (next_run_at)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS job_tasks (
    id               BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    job_id           BIGINT UNSIGNED NOT NULL,
    name             VARCHAR(128)    NOT NULL,
    `order`          INT             NOT NULL,
    handler_type     VARCHAR(256)    NOT NULL,
    plugin_assembly  VARCHAR(256)    NOT NULL,
    parameters_json  JSON            NOT NULL,
    timeout_seconds  INT             NOT NULL,
    max_retry_count  INT             NOT NULL,
    created_at       DATETIME(6)     NOT NULL,
    updated_at       DATETIME(6)     NOT NULL,
    deleted_at       DATETIME(6)     NULL,
    PRIMARY KEY (id),
    UNIQUE KEY uk_job_tasks_job_order (job_id, `order`),
    KEY idx_job_tasks_deleted_at (deleted_at),
    CONSTRAINT FK_job_tasks_jobs_job_id FOREIGN KEY (job_id) REFERENCES jobs (id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS job_executions (
    id            BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    job_id        BIGINT UNSIGNED NOT NULL,
    status        INT             NOT NULL,
    triggered_at  DATETIME(6)     NOT NULL,
    started_at    DATETIME(6)     NULL,
    finished_at   DATETIME(6)     NULL,
    error_message TEXT            NULL,
    PRIMARY KEY (id),
    KEY idx_job_executions_job_id (job_id),
    KEY idx_job_executions_status (status),
    CONSTRAINT FK_job_executions_jobs_job_id FOREIGN KEY (job_id) REFERENCES jobs (id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS task_executions (
    id                BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    job_execution_id  BIGINT UNSIGNED NOT NULL,
    job_task_id       BIGINT UNSIGNED NOT NULL,
    status            INT             NOT NULL,
    attempt_count     INT             NOT NULL,
    started_at        DATETIME(6)     NULL,
    finished_at       DATETIME(6)     NULL,
    output_json       JSON            NULL,
    error_message     TEXT            NULL,
    PRIMARY KEY (id),
    KEY idx_task_executions_job_execution_id (job_execution_id),
    KEY idx_task_executions_job_task_id (job_task_id),
    CONSTRAINT FK_task_executions_job_executions_job_execution_id FOREIGN KEY (job_execution_id) REFERENCES job_executions (id) ON DELETE CASCADE,
    CONSTRAINT FK_task_executions_job_tasks_job_task_id FOREIGN KEY (job_task_id) REFERENCES job_tasks (id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
