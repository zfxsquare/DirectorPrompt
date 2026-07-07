# 知识系统

## 概述

知识系统管理世界的底层设定和规则, 是不可变的世界观基础。它取代 SillyTavern 的"世界书" (World Info / Lorebook), 但核心原则不同: 知识不可变, AI 不能写入知识系统。

## 知识 vs 记忆

| | 知识系统 | 记忆系统 |
|--|---------|---------|
| 本质 | 世界规则、底层设定 | 故事中发生过的事 |
| 可变性 | 不可变 | 动态变化 |
| 谁写入 | 人工编写 (可 AI 辅助) | AI 运行时生成 |
| 示例 | "精神力决定战斗力" | "张三在第 3 章背叛了主角" |

会变的东西属于记忆系统。例如"张三年前是队长, 现在不是"——这不是知识在变, 是发生了事件 (张三被开除), 事件是记忆。

## 知识条目模型

```
KnowledgeEntry {
    id:           long
    projectId:    long
    title:        string         // "魔法体系" / "张三的背景"
    content:      string         // 知识正文
    tags:         string[]       // 辅助分类和检索
    groupId:      long?          // 所属分组, 可批量开关
    active:       boolean        // 是否参与检索
    embedding:    blob           // 向量, 入库时生成
    createdAt:    datetime
    updatedAt:    datetime
}
```

没有 entryType 分类, 没有时间有效性字段, 没有 source 区分 (因为 AI 不写入)。

## 知识分组

```
KnowledgeGroup {
    id:           long
    projectId:    long
    name:         string         // "前期设定" / "后期世界观" / "隐藏真相"
    description:  string?        // 分组说明
    active:       boolean        // 批量开关
}
```

分组是用户手动管理的, AI 不参与开关决策。这给了用户对世界观演变的完全控制权 — 叙诡、反转、设定揭示, 都由用户手动控制时机。

### 使用场景

废柴逆袭流: "后期世界观"分组一开始关闭, 包含"主角创造新世界后的规则"等知识。剧情推进到相应阶段时, 用户手动打开分组, 新规则生效。

## 检索策略: 两路检索 + Phase 启用

每轮叙事前, Knowledge Agent 用当前场景信息做两路并行检索, 合并去重后注入 Narrator 上下文。Phase 机制不独立成路, 而是扩大检索池 — Phase 激活的知识与正常活跃知识一同进入候选池, 由两路检索统一筛选。

### 第一路: 语义检索

当前场景描述 → Embedding 模型 → 查询向量 → sqlite-vec top-K 近邻搜索。

```sql
SELECT k.id, k.title, k.content, e.distance
FROM knowledge_embeddings e
JOIN knowledge_entries k ON k.id = e.entry_id
WHERE k.project_id = @ProjectId
  AND k.active = 1
  AND (k.group_id IS NULL OR 
       (SELECT active FROM knowledge_groups WHERE id = k.group_id) = 1)
  AND e.embedding MATCH @QueryVec
ORDER BY e.distance
LIMIT @TopK
```

解决"巷子 vs 窄道"的语义匹配问题。

### 第二路: 实体检索

知识条目的 title + tags 作为实体索引。从叙事文本中匹配包含的实体名, 精确注入关联知识。

实体检索不依赖向量, 是精确匹配。解决向量检索可能漏掉精确匹配的重要信息的问题 — 比如叙事中提到"张三", 张三的背景知识必须注入。

```
KnowledgeEntityIndex {
    entryId: long
    entityName: string      // "张三" / "港口" / "魔法体系"
}

SELECT DISTINCT k.*
FROM knowledge_entries k
JOIN knowledge_entity_index i ON i.entry_id = k.id
WHERE k.project_id = @ProjectId
  AND k.active = 1
  AND i.entity_name IN (@ExtractedEntities)
```

实体名提取用简单方案: 用知识条目的 title 和 tags 建反向索引, 检索时在叙事文本里做字符串包含检查。零成本, 后续可增强为 AI NER。

### Phase 知识启用

状态系统的 Phase 机制在状态值满足条件时, 将关联的禁用知识变为可检索状态。这些知识进入语义检索和实体检索的候选池, 与正常活跃知识一同参与匹配。Phase 只控制“可不可被检索”, 不强制注入 — 能否被检索到取决于 Knowledge Agent 的语义匹配和实体匹配。

比如天气为“暴风雨”时, Phase 表达式 `{val} == "暴风雨"` 匹配, 关联的禁用知识条目变为可检索。如果叙事场景与暴风雨相关, Knowledge Agent 会通过语义检索或实体检索命中这些知识; 如果不相关, 则不会检索到。

### 合并策略

```
Phase 评估 → 启用关联知识 → 进入检索池
    │
    ▼
两路检索 (在活跃知识 + Phase 启用知识的合并池上执行)
    │
    ├─ 语义检索 top-K (如 K=8)
    ├─ 实体检索命中 (精确匹配)
    │
    ▼
去重 (按 entryId)
    │
    ▼
按优先级排序:
    1. 实体检索命中 (高, 精确匹配)
    2. 语义检索 (按 distance 排序)
    │
    ▼
Token 预算截断
    (按优先级填入, 超出的丢弃)
```

## Token 预算管理

```
KnowledgeRetrievalConfig {
    semanticTopK: int          // 语义检索取几条, 默认 8
    tokenBudget: int           // 总 token 预算, 默认 2000
    minRelevance: float        // 语义检索的最低相关性阈值, 低于此值不注入
}
```

截断逻辑: 按优先级排序后逐条填入, 累计 token 数达到预算就停。

## AI 的角色

AI 在知识系统中的职责仅限读取:

- **Knowledge Agent**: 每轮叙事前执行三路检索, 合并结果注入 Narrator 上下文
- **Narrator**: 可以主动调用 `query_knowledge` 查询不确定的设定细节
- **AI 辅助编写** (编辑器中): 用户写一段设定文本, AI 帮助拆分、润色、生成 tags — 这是编辑器功能, 不是运行时行为, 产出仍由用户确认保存

AI 不能做的事:

- 不能 add_knowledge
- 不能 update_knowledge
- 不能开关分组
- 不能修改 active 状态

## 工具定义

### 查询 (AI 可用)

```
query_knowledge(query: string, topK: int?) -> [{ title, content, tags, relevance }]
```

### 写入/修改

知识条目的增删改全部通过 UI 手动操作, 不作为 tool 暴露给 AI。AI 辅助编写是编辑器内的功能, 不是运行时 tool。

## 与其他系统的交互

| 系统 | 交互方式 |
|------|---------|
| 状态系统 | Phase 激活时将关联的禁用知识变为可检索, 由 Knowledge Agent 语义匹配决定是否检索 |
| 记忆系统 | 无交互 — 知识是规则, 记忆是事件, 两者独立 |
| 时间线 | 无交互 — 知识没有时间有效性, 分组开关替代时间过滤 |
| 审计系统 | Audit Agent 用知识条目校验叙事是否违反世界设定 |
| Agent 编排 | Knowledge Agent 只读检索, 不写入 |
