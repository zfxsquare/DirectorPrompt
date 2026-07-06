# 时间线系统

## 概述

时间线系统为故事提供一个可排序的绝对时间坐标轴。每个场景是坐标轴上的一个节点, 拥有一个绝对坐标 (`timelinePosition`)。本地代码通过整数比较直接排序, 不依赖任何相对关系或语义解析。

## 场景模型

```
Scene {
    id:               long       // 自增, 追加顺序 = 阅读顺序
    projectId:        long
    timelinePosition: long       // 故事时间轴上的绝对坐标, 可排序
    timeLabel:        string     // 语义时间, "第一天傍晚" / "三年前的雨夜"
    summary:          string?    // 场景结束后由 Memory Agent 生成
    status:           enum       // active / completed / archived
}
```

场景是时间线上的一个节点, 一个容器。场景本身只携带时间信息, 不描述地点、人物等设定。其他系统按需挂载到场景上:

| 系统 | 挂载方式 |
|------|---------|
| 状态系统 | 状态变更日志引用 sceneId |
| 知识系统 | 知识条目的 validFrom / validTo 指向 timelinePosition |
| 记忆系统 | 场景摘要本身是一个记忆条目, 引用 sceneId |
| 人物系统 | 人物出场标记, 引用 sceneId |

## 坐标轴规则

坐标轴是一维数轴, 以 10 万为初始步长:

- 首个场景: `timelinePosition = 0`
- 首个场景之前插入新场景: `timelinePosition = -100000`
- 首个场景之后追加新场景: `timelinePosition = +100000`
- 两个场景之间插入: 取中位数 `(prev.timelinePosition + next.timelinePosition) / 2`
- 最末尾场景追加: `timelinePosition = 末尾场景.timelinePosition + 100000`

步长常量 `GAP = 100000`。中点法在同一区间连续插入的极限约为 log2(100000) ≈ 17 次。耗尽时拒绝在该区间创建新场景。如未来需要更大空间, 更新常量即可 (10 万 → 100 万 → 1000 万)。

## 两个排序维度

| 排序方式 | 字段 | 用途 |
|---------|------|------|
| 阅读顺序 | `id` (追加顺序) | UI 对话流显示、回滚定位、记忆压缩 |
| 故事时间顺序 | `timelinePosition` | 审计时间一致性、知识有效性过滤、记忆召回时间相关性 |

线性故事中两者一致。闪回时两者不同 — 闪回场景的 `id` 排在后面 (后创建), 但 `timelinePosition` 排在前面 (时间更早)。

## 场景创建

### 创建方式

场景切换由用户手动触发, 始终需要自然语言描述。有三种创建路径:

**1. AI 自主定位**

用户不指定位置, 只给自然语言描述 (如"跳到三年前的雨夜")。AI 调用 `query_scene` 查询现有场景列表, 判断新场景应放在哪, 然后调用 `create_scene`。

**2. 用户指定位置**

用户通过 UI 下拉框选择"在场景 A 之后"或"在场景 A 和场景 B 之间"。此时 AI 不需要查询和填写位置信息, 只需要根据自然语言描述生成 `timeLabel`。

**3. 用户全手动**

用户既指定位置又填写 `timeLabel`, AI 完全不参与场景创建, 系统直接写入。

### 工具: query_scene

让 AI 查询现有场景列表, 获取每个场景的 id、timelinePosition、timeLabel、status。AI 据此判断新场景应该放在哪两个场景之间 (或最前/最后)。

### 工具: create_scene

```
create_scene(
    afterSceneId:  long?,    // 新场景在时间轴上位于此场景之后
    beforeSceneId: long?,    // 新场景在时间轴上位于此场景之前
    timeLabel:     string    // 语义时间
) -> sceneId
```

`afterSceneId` 和 `beforeSceneId` 至少填一个:

| 填法 | 含义 | timelinePosition 计算 |
|------|------|----------------------|
| 都填 | 插入到两者之间 | `(after.timelinePosition + before.timelinePosition) / 2` |
| 只填 afterSceneId | 在此场景之后追加 | 若 after 是末尾: `after.timelinePosition + GAP`; 若 after 后面还有场景: 拒绝 (应该用两者都填) |
| 只填 beforeSceneId | 在此场景之前插入 | 若 before 是开头: `before.timelinePosition - GAP`; 若 before 前面还有场景: 拒绝 (应该用两者都填) |
| 都不填 | 项目首个场景 | `0` |

### 本地校验规则

- `timelinePosition` 不与同项目下已有场景重复
- 两者都填时: `afterSceneId.timelinePosition < beforeSceneId.timelinePosition`
- 两者都填时: 两者之间的空间足够取中点 (不耗尽)
- `timeLabel` 非空

校验不通过 → 返回错误信息 → AI 重新调用。不存在需要特殊处理的"幻觉", 就是参数校验拒绝重填。

## AI 的角色

AI 在时间线系统中的职责:

- **判断语义**: 根据 NATURAL 语言描述理解时间跨度, 生成 `timeLabel`
- **查询场景**: 调用 `query_scene` 获取现有坐标空间, 判断新场景的位置
- **填写 ID**: 调用 `create_scene` 填写 `afterSceneId` / `beforeSceneId`

AI 不做的事:

- 不填写 `timelinePosition` 数值
- 不计算中点
- 不做位置校验

数值计算和校验全部由本地代码完成。

## 线性与非线性叙事

### 线性叙事 (默认)

场景按时间顺序追加, `timelinePosition` 单调递增, `ORDER BY id` 和 `ORDER BY timeline_position` 结果一致。

### 非线性叙事 (闪回等)

用户描述"回到三年前的雨夜", AI 判断这是闪回, 调用 `query_scene` 查询后, 将新场景插入到对应位置 (timelinePosition 较小)。

闪回场景的处理规则:

- **状态系统**: 闪回中的状态变更不生效 (不能在闪回里改变当前状态)
- **知识系统**: 闪回中揭示的信息可加入知识库, 标记为历史事件
- **记忆系统**: 闪回场景同样生成摘要, 存入记忆库
- 闪回结束后, 用户手动创建新场景回到当前时间点

### 场景时间线展示

UI 可提供时间线视图, 按 `timelinePosition` 排列所有场景, 用户直观看到故事的时间结构:

```
时间轴 (timelinePosition 排序):
  ─── 场景4(-100000) "三年前" ─── 场景1(0) "第一天" ─── 场景2(100000) "深夜" ─── 场景3(200000) "三天后" ───
```

## 知识系统的时间有效性

知识条目用 `timelinePosition` 做时间有效性, 纯整数比较:

```
KnowledgeEntry {
    ...
    validFrom: int?     // timelinePosition, null = 从故事开始有效
    validTo: int?       // timelinePosition, null = 当前仍然有效
}

检索时:
WHERE (valid_from IS NULL OR valid_from <= @CurrentTimelinePos)
  AND (valid_to IS NULL OR valid_to > @CurrentTimelinePos)
```

本地代码直接比较, 不需要 LLM 理解时间先后。

## 场景生命周期

| 状态 | 含义 |
|------|------|
| active | 当前正在进行叙事的场景, 同一时间只有一个 |
| completed | 已结束, 已生成摘要, 原文可归档 |
| archived | 原文已从活跃存储移除, 仅保留摘要 |

场景从 active → completed 在场景切换时发生。completed → archived 在记忆压缩达到一定深度后可选发生。
