-- 对话
CREATE TABLE IF NOT EXISTS sessions
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
    title
    TEXT
    NOT
    NULL
    DEFAULT
    '',
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

CREATE INDEX IF NOT EXISTS idx_sessions_project ON sessions(project_id);

-- 为现有项目创建默认对话
INSERT INTO sessions (project_id, title, created_at, updated_at)
SELECT id, '默认对话', datetime('now'), datetime('now')
FROM projects;

-- scenes: 添加 session_id
ALTER TABLE scenes
    ADD COLUMN session_id INTEGER;
UPDATE scenes
SET session_id = (SELECT id FROM sessions WHERE sessions.project_id = scenes.project_id LIMIT 1);
CREATE INDEX IF NOT EXISTS idx_scenes_session ON scenes(session_id);

-- playthrough_events: 添加 session_id
ALTER TABLE playthrough_events
    ADD COLUMN session_id INTEGER;
UPDATE playthrough_events
SET session_id = (SELECT id FROM sessions WHERE sessions.project_id = playthrough_events.project_id LIMIT 1);
CREATE INDEX IF NOT EXISTS idx_events_session ON playthrough_events(session_id);

-- memory_entries: 添加 session_id
ALTER TABLE memory_entries
    ADD COLUMN session_id INTEGER;
UPDATE memory_entries
SET session_id = (SELECT id FROM sessions WHERE sessions.project_id = memory_entries.project_id LIMIT 1);
CREATE INDEX IF NOT EXISTS idx_memory_session ON memory_entries(session_id);

-- active_directives: 添加 session_id
ALTER TABLE active_directives
    ADD COLUMN session_id INTEGER;
UPDATE active_directives
SET session_id = (SELECT id FROM sessions WHERE sessions.project_id = active_directives.project_id LIMIT 1);
CREATE INDEX IF NOT EXISTS idx_directives_session ON active_directives(session_id);

-- state_snapshots: 添加 session_id
ALTER TABLE state_snapshots
    ADD COLUMN session_id INTEGER;
UPDATE state_snapshots
SET session_id = (SELECT id FROM sessions WHERE sessions.project_id = state_snapshots.project_id LIMIT 1);
CREATE INDEX IF NOT EXISTS idx_snapshots_session ON state_snapshots(session_id);

-- characters: 添加 session_id
ALTER TABLE characters
    ADD COLUMN session_id INTEGER;
UPDATE characters
SET session_id = (SELECT id FROM sessions WHERE sessions.project_id = characters.project_id LIMIT 1);
CREATE INDEX IF NOT EXISTS idx_characters_session ON characters(session_id);

-- character_relations: 添加 session_id
ALTER TABLE character_relations
    ADD COLUMN session_id INTEGER;
UPDATE character_relations
SET session_id = (SELECT id FROM sessions WHERE sessions.project_id = character_relations.project_id LIMIT 1);
CREATE INDEX IF NOT EXISTS idx_relations_session ON character_relations(session_id);

-- state_values: 重建表以支持 session_id
CREATE TABLE IF NOT EXISTS state_values_new
(
    attribute_id
    INTEGER
    NOT
    NULL,
    session_id
    INTEGER
    NOT
    NULL,
    value
    TEXT
    NOT
    NULL
    DEFAULT
    '',
    updated_at
    TEXT
    NOT
    NULL,
    PRIMARY
    KEY
(
    attribute_id,
    session_id
),
    FOREIGN KEY
(
    attribute_id
) REFERENCES state_attributes
(
    id
),
    FOREIGN KEY
(
    session_id
) REFERENCES sessions
(
    id
)
    );

INSERT INTO state_values_new (attribute_id, session_id, value, updated_at)
SELECT sv.attribute_id,
       COALESCE((SELECT id FROM sessions WHERE sessions.project_id = sa.project_id LIMIT 1), 0),
       sv.value,
       sv.updated_at
FROM state_values sv
         JOIN state_attributes sa ON sa.id = sv.attribute_id;

DROP TABLE state_values;
ALTER TABLE state_values_new RENAME TO state_values;

-- flags: 重建表以支持 session_id
CREATE TABLE IF NOT EXISTS flags_new
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
    INTEGER
    NOT
    NULL,
    name
    TEXT
    NOT
    NULL,
    display_name
    TEXT
    NOT
    NULL
    DEFAULT
    '',
    value
    INTEGER
    NOT
    NULL
    DEFAULT
    0,
    set_at_scene_id
    INTEGER,
    FOREIGN
    KEY
(
    project_id
) REFERENCES projects
(
    id
),
    FOREIGN KEY
(
    session_id
) REFERENCES sessions
(
    id
)
    );

INSERT INTO flags_new (project_id, session_id, name, display_name, value, set_at_scene_id)
SELECT project_id,
       COALESCE((SELECT id FROM sessions WHERE sessions.project_id = flags.project_id LIMIT 1), 0),
       name,
       display_name,
       value,
       set_at_scene_id
FROM flags;

DROP TABLE flags;
ALTER TABLE flags_new RENAME TO flags;

CREATE UNIQUE INDEX IF NOT EXISTS idx_flags_session_name ON flags(session_id, name);

-- composite_items: 添加 session_id
ALTER TABLE composite_items
    ADD COLUMN session_id INTEGER;
UPDATE composite_items
SET session_id = COALESCE(
    (SELECT s.id
     FROM sessions s
              JOIN state_attributes sa ON sa.project_id = s.project_id
     WHERE sa.id = composite_items.attribute_id LIMIT 1), 0);
CREATE INDEX IF NOT EXISTS idx_composite_items_session ON composite_items(session_id);

-- state_change_logs: 添加 session_id
ALTER TABLE state_change_logs
    ADD COLUMN session_id INTEGER;
UPDATE state_change_logs
SET session_id = COALESCE(
    (SELECT s.id
     FROM sessions s
              JOIN state_attributes sa ON sa.project_id = s.project_id
     WHERE sa.id = state_change_logs.attribute_id LIMIT 1), 0);
