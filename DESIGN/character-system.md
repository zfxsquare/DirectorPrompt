# 人物系统

## 概述

人物系统管理叙事中的角色实体。人物是动态的 — 运行时可增删, 状态可变化, 关系可演变。这与知识系统 (不可变) 和记忆系统 (事件记录) 不同, 人物系统管理的是角色的当前状态。

人物的维护 (增删改、状态更新、关系变更) 由 Memory Sub-Agent 在叙事生成后执行, Narrator Agent 不参与。

## 分类系统

### 分类模型

分类采用接口式多继承 — 一个人物可属于多个分类, 分类之间有继承关系:

```
CharacterCategory {
    id:                long
    projectId:         long
    name:              string         // "工作人员" / "顾客" / "未分类"
    description:       string?
    parentCategoryIds: long[]         // 父分类, 支持多继承
}
```

### 继承示例

```
未分类 (base)
├─ 顾客 (继承未分类)
│   ├─ 贵宾顾客 (继承顾客)
│   └─ 普通顾客 (继承顾客)
├─ 工作人员 (继承未分类)
│   ├─ 店员 (继承工作人员)
│   └─ 管理人员 (继承工作人员)
└─ 朋友 (继承未分类)
```

人物 A 同时属于"店员"和"朋友", 继承链:

- 店员 → 工作人员 → 未分类
- 朋友 → 未分类

### 分类管理

分类的定义和管理由用户在 UI 中完成, AI 不参与 — 分类是剧本结构的一部分, 不是运行时动态创建的。

## 状态栏: 复用全局状态系统

人物的状态栏与全局状态系统复用同一套 StateAttribute 模型, 通过 `scope` 区分作用域:

```
StateAttribute {
    ...
    scope:        enum          // global | category
    categoryId:   long?         // scope=category 时, 属于哪个分类
}
```

| | 全局状态 | 人物状态 |
|--|---------|---------|
| scope | global | category |
| 作用域 | 整个项目 | 特定分类的人物 |
| 存储 | StateValue 表 | CharacterStateValue 表 |
| 检索 | 直接查 | 通过人物的分类归属解析后查 |

### 继承解析

父分类的状态属性自动被子分类继承。子分类可以扩展 (新增属性), 也可以覆盖 (同名属性用子分类的配置)。

人物 A 属于"店员"和"朋友", 状态属性并集:

```
未分类定义的:     [基础属性]
工作人员扩展的:   [薪资, 排班状态]
店员扩展的:       [销售业绩]
朋友扩展的:       [好感度]

最终人物 A 的状态栏 = [基础属性] + [薪资, 排班状态] + [销售业绩] + [好感度]
```

### 解析缓存

```
CharacterResolvedCategories {
    characterId:    long
    categoryIds:    long[]       // 展开后的所有分类 (含祖先), 去重
    attributeIds:   long[]       // 展开后的所有状态属性, 去重, 处理覆盖
}
```

分类变更时重新计算缓存。

### driver 一致性

人物状态的 driver 行为与全局状态完全一致:

- narrative 驱动: Memory Sub-Agent 从叙事中提取变更, 调用 tool 落地
- system 驱动: 系统按配置规则自动变换, 通过 Phase 将关联知识变为可检索

权限矩阵也一致 — narrative 驱动 AI 可改, system 驱动 AI 不可直接改。

## 人物模型

```
Character {
    id:            long
    projectId:     long
    name:          string
    description:   string
    categoryIds:   long[]        // 属于哪些分类
    status:        enum          // active / left / dead
    createdAt:     datetime
    updatedAt:     datetime
}

CharacterStateValue {
    characterId:   long
    attributeId:   long          // 来自分类解析出的状态属性
    value:         string        // 当前值
    updatedAt:     datetime
}
```

## 人物在场追踪

```
CharacterScenePresence {
    characterId:  long
    sceneId:      long
}
```

查询当前场景在场人物:

```sql
SELECT c.* FROM characters c
JOIN character_scene_presence p ON p.character_id = c.id
WHERE p.scene_id = @CurrentSceneId
  AND c.status = 'active'
```

在场管理由 Memory Sub-Agent 通过 tool call 执行 (enter_scene / leave_scene), Narrator 不参与。

## 人物关系网络

关系是人物系统的一等子功能, 不是独立系统。关系离不开人物, 查询起点始终是人物, 生命周期与人物绑定。

### 关系模型

关系是**有向的** — A 对 B 的关系和 B 对 A 的关系可以不同:

```
CharacterRelation {
    id:              long
    projectId:       long
    characterIdA:    long          // 主体
    characterIdB:    long          // 客体
    relationType:    string        // "仇恨" / "师徒" / "恋人" / "雇佣"
    description:     string?       // "因港口大火事件结仇"
    intensity:       float?        // 可选, 关系强度 0-1
    createdAt:       datetime
    updatedAt:       datetime
}
```

示例:

```
张三 → 李四: 仇恨 (因港口大火)
李四 → 张三: 愧疚 (其实是误会)
```

### 关系变更追踪

```
CharacterRelationLog {
    relationId:     long
    oldType:        string?
    newType:        string
    oldDescription: string?
    newDescription: string?
    source:         enum          // memory_sub_agent / director_manual
    reason:         string
    sceneId:        long
    createdAt:      datetime
}
```

### 关系与记忆的联动

关系变更本身产生一条记忆:

```
第 5 章场景摘要:
  "张三得知真相后, 对李四的关系从'愧疚'变为'愤怒'"
  relatedCharacterIds: [张三, 李四]
  tags: ["关系变化", "冲突"]
```

审计追踪是结构化记录, 记忆是叙事性记录, 两者互补。

### 关系网络注入

Narrator 生成叙事前, 当前场景在场人物的关系网络由 Orchestrator 确定性注入, 不需要 AI 参与:

```
注入 Narrator 上下文:
  当前场景人物: 张三, 李四
  关系:
    张三 → 李四: 仇恨 (因港口大火)
    李四 → 张三: 愧疚 (其实是误会)
```

## 记忆系统结合

MemoryEntry 增加 `relatedCharacterIds` 字段, 由 Memory Sub-Agent 在生成/更新记忆时填写:

```
MemoryEntry {
    ...
    relatedCharacterIds: long[]     // 这条记忆涉及哪些人物
}
```

### 查询人物相关记忆

```sql
SELECT m.* FROM memory_entries m
WHERE m.project_id = @ProjectId
  AND m.timeline_pos <= @CurrentTimelinePos
  AND EXISTS (
    SELECT 1 FROM json_each(m.related_character_ids) 
    WHERE value = @CharacterId
  )
ORDER BY m.timeline_pos DESC
```

### 与向量检索配合

Memory Sub-Agent 召回记忆时, 叠加人物过滤:

```
场景: 张三和李四在港口交谈
    │
    ▼
Memory Sub-Agent:
    语义检索: 当前场景描述 → top-K
    人物过滤: relatedCharacterIds 包含 [张三ID, 李四ID] → 补充召回
    合并 → 综合摘要 → 返回 Narrator
```

既按语义相关性召回, 又确保涉及当前在场人物的记忆不被遗漏。

### 人物档案

用户在 UI 中点击某个人物, 可查看该人物的所有相关记忆 — 按时间线排列, 形成人物经历档案。

## AI 的角色

人物维护由 Memory Sub-Agent 在叙事生成后执行, Narrator 不参与:

```
叙事生成完成
    │
    ▼
Memory Sub-Agent 审查新叙事:
    输入: 新叙事文本 + 当前人物列表 + 在场人物 (短上下文)
    │
    ├─ 新人物出现 → add_character
    ├─ 人物描述更新 → update_character
    ├─ 人物离场/死亡 → remove_character
    ├─ 人物进入/离开场景 → enter_scene / leave_scene
    ├─ 人物状态变化 → update_character_state
    └─ 关系变化 → set_relation
    │
    ▼
系统执行, 记录审计追踪
```

Narrator Agent 不直接接触任何人物工具。人物的维护全部由 Memory Sub-Agent 在 Orchestrator 的调度下完成。

## 工具定义

### 查询

```
get_character(name: string) -> { name, description, categories, status, stateValues, relations }
get_scene_characters() -> [{ name, description, categories, status }]
get_relations(characterName: string) -> [{ target, type, description, direction }]
get_character_state(characterName: string, attribute: string) -> { value }
```

### 管理 (Memory Sub-Agent 内部使用, 不暴露给 Narrator)

```
add_character(name: string, description: string, categoryIds: long[], reason: string) -> characterId
remove_character(name: string, reason: string)
update_character(name: string, description: string?, reason: string)
set_relation(characterA: string, characterB: string, relationType: string, description: string?, reason: string)

// 人物状态变更 (复用全局状态系统的 driver 逻辑)
update_character_state(characterName: string, attribute: string, delta: float, reason: string)
set_character_state(characterName: string, attribute: string, value: string, reason: string)

// 在场管理
enter_scene(characterName: string)
leave_scene(characterName: string)
```

### 分类管理 (UI 操作, 不暴露给 AI)

```
create_category(name: string, parentIds: long[], description: string?)
update_category(categoryId: long, ...)
```

## 与其他系统的交互

| 系统 | 交互方式 |
|------|---------|
| 状态系统 | 复用 StateAttribute 模型, scope=category; driver 行为一致; 权限矩阵一致 |
| 记忆系统 | MemoryEntry 关联 relatedCharacterIds; 记忆可按人物过滤召回; 人物档案视图 |
| 时间线 | 在场追踪按 sceneId; 回滚时清理该场景及之后的在场记录 |
| 知识系统 | 知识条目提供人物固有设定背景, 人物系统提供运行时状态, 检索时两边都查 |
| 审计系统 | Audit Agent 校验"已死人物是否重新出现"等; 人物状态变更可审计 |
| Agent 编排 | Memory Sub-Agent 在 Narrator 后维护人物; Narrator 不接触人物工具 |
