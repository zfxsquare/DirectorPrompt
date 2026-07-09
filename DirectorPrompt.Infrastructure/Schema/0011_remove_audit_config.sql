-- 移除审计配置: 重建 projects 表去掉 audit_config 列

CREATE TABLE IF NOT EXISTS projects_new
(
    id
    INTEGER
    PRIMARY
    KEY
    AUTOINCREMENT,
    name
    TEXT
    NOT
    NULL,
    description
    TEXT
    NOT
    NULL
    DEFAULT
    '',
    opening_message
    TEXT
    NOT
    NULL
    DEFAULT
    '',
    memory_config
    TEXT
    NOT
    NULL
    DEFAULT
    '{}',
    knowledge_config
    TEXT
    NOT
    NULL
    DEFAULT
    '{}',
    created_at
    TEXT
    NOT
    NULL,
    updated_at
    TEXT
    NOT
    NULL
);

INSERT INTO projects_new
(id, name, description, opening_message, memory_config, knowledge_config, created_at, updated_at)
SELECT id,
       name,
       description,
       opening_message,
       memory_config,
       knowledge_config,
       created_at,
       updated_at
FROM projects;

DROP TABLE projects;
ALTER TABLE projects_new RENAME TO projects;
