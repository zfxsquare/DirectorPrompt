-- 移除快照系统: 删除 state_snapshots 表及相关索引

DROP INDEX IF EXISTS idx_snapshots_project_round;
DROP INDEX IF EXISTS idx_snapshots_scene;
DROP INDEX IF EXISTS idx_snapshots_session;
DROP TABLE IF EXISTS state_snapshots;
