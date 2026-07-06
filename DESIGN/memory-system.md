# 记忆系统

## 概述

记忆系统记录故事中发生过的事。与知识系统完全对称:

| | 知识系统 | 记忆系统 |
|--|---------|---------|
| 本质 | 不变的世界规则 | 变化的故事事件 |
| 写入者 | 人工编写 (可 AI 辅助) | AI 运行时生成 |
| 可变性 | 不可变 | 可改写更新 |

记忆系统的核心问题: 故事越来越长, 不可能把所有历史塞进上下文。需要把旧历史压缩成摘要存储, 并在需要时按相关性召回。

## 记忆模型

```
MemoryEntry {
    id:                  long
    projectId:           long
    sceneId:             long          // 必填, 归属场景
    timelinePos:         int           // 场景的 timelinePosition, 用于过滤和衰减
    content:             string        // 记忆正文 (自然语言或结构化, 视内容而定)
    tags:                string[]      // 必填, 用于分类和筛选
    relatedCharacterIds: long[]        // 这条记忆涉及哪些人物
    embedding:           blob          // 向量, 入库时生成
    createdAt:           datetime
    updatedAt:           datetime      // 改写更新时变化
}
```

### 人物关联

`relatedCharacterIds` 由 Memory Sub-Agent 在生成/更新记忆时填写 — 它阅读叙事内容, 识别涉及的人物, 写入关联。这使得记忆可以按人物过滤召回, 也支持人物档案视图 (某人物的所有相关记忆按时间线排列)。详见人物系统文档。

### 归属场景

所有记忆必须归属于某个场景。开局就带的背景记忆 (如"老王是个侦探") 归属于一个特殊的初始场景, `timelinePosition = 0`。

### 召回过滤

只召回当前时间点之前的记忆:

```sql
WHERE m.project_id = @ProjectId
  AND m.timeline_pos <= @CurrentTimelinePos
```

这天然处理了所有时间线问题 — 不需要特殊标记闪回, 不需要 validFrom/validTo。当前场景在哪个时间点, 就只能看到那个时间点及之前的记忆。

## 记忆的创建

### 场景摘要

场景切换时, 子 Agent 为旧场景生成摘要:

```
场景切换发生
    │
    ▼
Memory Sub-Agent 处理旧场景:
    输入: 旧场景的所有回合原文 + 当前状态
    输出: 场景摘要 (content + tags)
    │
    ▼
摘要存入记忆库:
    - 生成 embedding
    - 关联 sceneId
    - 记录 timelinePos
```

摘要内容涵盖场景的时间、地点、关键事件、状态变化、重要人物互动。格式可以是自然语言也可以是结构化文本, 视内容而定 — 有些天然适合结构化, 有些天然适合自然语言。

### 背景记忆

项目创建时, 用户可以预设背景记忆 (如角色背景、历史事件等)。这些记忆归属于初始场景 (`timelinePosition = 0`), 从故事一开始就可以被召回。

## 记忆的改写更新

记忆不是只读的归档, 而是可以被改写更新的活数据。故事发展可能改变对过去事件的理解:

| 场景 | 操作 |
|------|------|
| 新事件改变了对旧事件的理解 | 改写旧记忆的内容 (如"张三是好人" → "张三的善意是伪装") |
| 新信息补充了旧记忆 | 扩展旧记忆的内容 (如"张三是侦探" → "张三是侦探, 三年前在港口大火中失去搭档") |
| 多条记忆描述同一事件 | 合并为一条更精炼的记忆 |
| 审计发现记忆不准确 | 修正记忆内容 |

改写更新由 Memory Sub-Agent 在叙事生成后执行, 与 Narrator Agent 完全无关:

```
叙事生成完成
    │
    ▼
Memory Sub-Agent 审查新叙事:
    输入: 新叙事文本 + 相关已有记忆 (短上下文)
    输出: 记忆更新操作 (新建 / 改写 / 合并)
    │
    ▼
系统执行更新, 重新生成 embedding
```

### 不做激进归档

记忆不会被标记为 archived 并从检索中移除。所有记忆始终可被召回, 通过自然权重衰减让旧记忆在排序中自然下沉。如果记忆内容过时, 通过改写更新修正, 而不是隐藏。

## 记忆召回: Sub-Agent 模式

### 核心原则

记忆召回不由 Narrator Agent 通过 tool call 完成。而是由 Orchestrator 在 Narrator 生成前, 委托一个 Memory Sub-Agent 执行检索和综合:

```
Narrator 需要生成叙事
    │
    ▼
Orchestrator 委托 Memory Sub-Agent:
    输入: 当前场景信息 (短上下文)
    │
    ├─ Sub-Agent 查询记忆库 (向量检索 + tag 过滤)
    ├─ Sub-Agent 综合检索结果
    └─ Sub-Agent 返回精炼的记忆摘要
    │
    ▼
精炼摘要注入 Narrator 上下文的 ⑥ Recalled Memory 部分
    │
    ▼
Narrator 带着干净上下文生成叙事
```

### 为什么用 Sub-Agent 模式

- **Narrator 上下文干净**: 不携带一堆 tool call 和 tool result, 减少上下文污染
- **Sub-Agent 上下文短**: 只需要当前场景信息, 不需要完整历史
- **可用 Flash 模型**: 检索和综合不需要强推理能力, 用便宜快速的模型即可
- **职责分离**: Narrator 专注生成, Memory Sub-Agent 专注检索和综合

### 检索逻辑

Sub-Agent 内部的检索, 语义检索 + 人物过滤双路合并:

```sql
-- 语义检索
SELECT m.id, m.content, m.tags, m.related_character_ids, e.distance
FROM memory_embeddings e
JOIN memory_entries m ON m.id = e.entry_id
WHERE m.project_id = @ProjectId
  AND m.timeline_pos <= @CurrentTimelinePos
  AND e.embedding MATCH @QueryVec
ORDER BY (向量相似度 × 时间衰减权重) DESC
LIMIT @TopK

-- 人物过滤补充召回 (当前场景在场人物的相关记忆)
SELECT DISTINCT m.*
FROM memory_entries m
WHERE m.project_id = @ProjectId
  AND m.timeline_pos <= @CurrentTimelinePos
  AND EXISTS (
    SELECT 1 FROM json_each(m.related_character_ids)
    WHERE value IN (@SceneCharacterIds)
  )
```

Sub-Agent 可以进一步用 tag 过滤, 也可以多次检索不同角度的记忆, 最终综合成一份精炼摘要返回。两路结果合并去重, 确保涉及当前在场人物的记忆不被遗漏。

## 权重衰减

模拟人类记忆的自然衰减 — 旧记忆权重低, 但仍然可被召回:

```
最终得分 = 向量相似度 × 时间衰减权重

时间衰减权重 = exp(-λ × (currentTimelinePos - memory.timelinePos) / GAP)
```

- λ 是可配置的衰减系数, 默认让相邻场景衰减可忽略, 跨越多场景后明显衰减
- 衰减是连续的, 不是硬性的归档/不归档二元开关
- 频繁被召回的记忆可以考虑加权 (模拟"经常回忆的事更难忘"), 作为后续增强

## 配置

```
MemoryConfig {
    recallTopK: int           // 召回条数, 默认 10
    tokenBudget: int          // Sub-Agent 返回摘要的 token 预算, 默认 1500
    minRelevance: float       // 最低相关性阈值
    timeDecayLambda: float    // 时间衰减系数
}
```

## 工具定义

### Sub-Agent 内部使用 (不暴露给 Narrator)

```
query_memory(query: string, tags: string[]?, topK: int?) -> [{ content, tags, sceneId, relevance }]
```

### 记忆更新 (Sub-Agent 内部使用)

```
create_memory(sceneId: long, content: string, tags: string[]) -> memoryId
update_memory(memoryId: long, content: string, tags: string[]?) -> success
merge_memories(memoryIds: long[], content: string, tags: string[]) -> memoryId
```

Narrator Agent 不直接接触任何记忆工具。记忆的召回和更新全部由 Sub-Agent 在 Orchestrator 的调度下完成。

Memory Sub-Agent 同时负责人物系统的维护 (增删改、状态更新、关系变更), 详见人物系统文档。

## 与其他系统的交互

| 系统 | 交互方式 |
|------|---------|
| 时间线 | 记忆归属场景, 携带 timelinePos, 召回时按 timelinePos 过滤和衰减 |
| 状态系统 | 场景摘要参考当前状态; 记忆更新 Sub-Agent 可参考状态变更日志 |
| 知识系统 | 无交互 — 知识是规则, 记忆是事件, 两者独立检索, 各自注入 Narrator 上下文 |
| 人物系统 | MemoryEntry 关联 relatedCharacterIds, Sub-Agent 填写; 记忆按人物过滤召回; Sub-Agent 同时维护人物数据 |
| 审计系统 | Audit Agent 可查询记忆校验叙事一致性; 审计发现的不准确记忆由 Memory Sub-Agent 修正 |
| Agent 编排 | Memory Sub-Agent 在 Narrator 前执行召回, 在 Narrator 后执行记忆更新 + 人物维护, Narrator 不参与 |
