-- 移除人物级进入/退出指令: 重建 characters 表去掉 enter_directives 和 exit_directives 列

CREATE TABLE IF NOT EXISTS characters_new
(
    id
    INTEGER
    PRIMARY
    KEY
    AUTOINCREMENT,
    project_id
    INTEGER
    NOT
    NULL,
    session_id
    INTEGER,
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
    category_ids
    TEXT
    NOT
    NULL
    DEFAULT
    '[]',
    status
    TEXT
    NOT
    NULL
    DEFAULT
    'active',
    created_at
    TEXT
    NOT
    NULL,
    updated_at
    TEXT
    NOT
    NULL,
    FOREIGN
    KEY
(
    project_id
) REFERENCES projects
(
    id
)
    );

INSERT INTO characters_new (id, project_id, session_id, name, description, category_ids, status, created_at, updated_at)
SELECT id,
       project_id,
       session_id,
       name,
       description,
       category_ids,
       status,
       created_at,
       updated_at
FROM characters;

DROP TABLE characters;
ALTER TABLE characters_new RENAME TO characters;

CREATE INDEX IF NOT EXISTS idx_characters_project ON characters(project_id);
CREATE INDEX IF NOT EXISTS idx_characters_session ON characters(session_id);
CREATE UNIQUE INDEX IF NOT EXISTS idx_characters_project_name ON characters(project_id, name);
