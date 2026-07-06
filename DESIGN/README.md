# DirectorPrompt

## 这是什么

DirectorPrompt 是一个 AI 交互小说框架, 以桌面软件形式提供。用户不扮演任何角色, 不出现在剧情中, 而是作为"导演", 通过组合不同类型的指令推动故事朝特定方向发展, AI 负责生成叙事内容。

典型的 AI 交互小说有两种模式:

1. **角色扮演模式**: 用户扮演特定角色, AI 扮演特定角色, 双方互动
2. **导演模式**: 用户提供剧情框架和指令, AI 生成故事发展, 类似沙盒/SLG 游戏

DirectorPrompt 选择第二种。第一种模式 SillyTavern 等工具已经做得够好; 第二种模式缺乏一个有状态系统、记忆系统、知识系统和审计能力的专业框架。

## 为什么不用 SillyTavern

SillyTavern 作为 AI 交互小说框架已经过时:

- 不支持工具调用 (Tool Use / Function Calling)
- 不支持 MCP (Model Context Protocol)
- 没有记忆系统 / 召回机制
- 没有可以让 AI 维护的自定义状态系统
- 世界书 (World Info) 依赖关键词匹配, 语义相近但字面不同的内容无法触发
- 没有审计机制, 叙事一致性无法保证
- 没有时间线管理, 无法处理时间跳跃和闪回

DirectorPrompt 从零开始设计这些能力。

## 核心特性

### 导演模式

用户通过指令组合器发送指令, 指令分类包括剧情、基调、临时约束、时间/场景变更。指令的排列顺序携带语义, AI 按序理解。用户也可以对 AI 的输出进行删除、手动编辑、修正、重写等操作。

### 多 Agent 编排

不同任务由不同 Agent 负责, 各 Agent 可配置不同模型:

- **Narrator Agent**: 生成叙事文本, 大模型, 高温
- **Knowledge Agent**: 检索世界知识, 中小模型
- **Memory Sub-Agent**: 召回/更新记忆, 维护人物数据, 提取状态变更, Flash 模型
- **Audit Agent**: 审计叙事一致性, 中小模型, 低温
- **Scene Agent**: 场景创建和时间线管理, Flash 模型

### 时间线系统

故事时间建模为一维坐标轴, 每个场景是坐标轴上的节点。支持线性叙事和闪回, 本地代码通过整数坐标直接排序和过滤。

### 状态系统

全局世界状态, 驱动叙事走向。支持三种数据形态 (numeric / enum / composite) 和两种驱动方式 (narrative / system)。包含阶段效果、标记、变更审计。状态系统同时服务于全局状态和人物状态, 通过分类继承解析。

### 知识系统

不可变的世界底层设定。AI 只读, 不能写入。支持知识分组批量开关, 控制世界观演变 (如叙诡、反转、设定揭示的时机)。三路混合检索 (语义 + 实体 + 状态注入), 取代关键词匹配的世界书。

### 记忆系统

故事中发生过的事, 可改写更新。记忆必须归属场景, 召回时按时间线过滤。记忆召回和更新由 Memory Sub-Agent 完成, Narrator 不接触记忆工具, 上下文保持干净。模拟人类记忆的自然权重衰减。

### 人物系统

运行时可增删的人物实体。分类支持接口式多继承, 人物可属于多个分类。状态栏跟随分类走, 复用全局状态系统的 StateAttribute 模型。有向人物关系网络, 关系变更产生记忆。人物维护由 Memory Sub-Agent 负责。

### 审计系统

守护叙事一致性。分维度并行审计 (设定 / 状态 / 人物 / 时间 / 记忆), 每个维度独立看、上下文隔离。三级严重程度, 代码层过滤低级别噪音。支持阻断模式 (重生成) 和标记模式 (放行带警告)。

### 事件序列

一次游玩过程是一次可重放的事件序列。所有系统状态可从事件序列重建。回滚 = 移除事件。

### 工具与 MCP

内置工具集覆盖人物管理、状态管理、知识查询、记忆操作、场景控制。支持 MCP 双向集成 — 既是 MCP Server 对外暴露能力, 也是 MCP Client 连接外部服务获取扩展工具。

## 技术栈

| 维度 | 选型 |
|------|------|
| 运行时 | .NET 10 + C# 14 |
| UI | WPF + WPF-UI |
| 数据库 | SQLite (Microsoft.Data.Sqlite) |
| 数据访问 | Dapper |
| 向量检索 | sqlite-vec |
| AI 抽象 | Microsoft.Extensions.AI (IChatClient + IEmbeddingGenerator) |
| AI Provider | OpenAI 兼容 / Anthropic / Ollama (本地) |
| MVVM | CommunityToolkit.Mvvm |
| DI / 主机 | Microsoft.Extensions.Hosting |
| 日志 | Serilog |
| 弹性 | Polly |
| MCP | ModelContextProtocol |
| Markdown | Markdig → FlowDocument |

## 设计文档索引

| 文档 | 内容 |
|------|------|
| [tech-stack.md](tech-stack.md) | 技术栈选型与设计原则 |
| [core-interaction-paradigm.md](core-interaction-paradigm.md) | 导演模式、指令类型、回合模型 |
| [timeline-system.md](timeline-system.md) | 场景、坐标轴、时间线排序 |
| [state-system.md](state-system.md) | 状态属性、驱动方式、效果、标记 |
| [knowledge-system.md](knowledge-system.md) | 不可变知识、分组、三路混合检索 |
| [memory-system.md](memory-system.md) | 三层记忆、改写更新、Sub-Agent 模式、权重衰减 |
| [character-system.md](character-system.md) | 分类继承、状态栏复用、关系网络、记忆结合 |
| [audit-system.md](audit-system.md) | 上下文隔离、分维度并行、严重程度过滤 |
| [agent-orchestration.md](agent-orchestration.md) | 事件序列、流水线、指令组合、输出操作 |

## 设计原则

- **核心逻辑与 UI 分离**: 领域模型 + Agent 编排不依赖 UI 和具体存储实现, 可独立测试
- **AI 调用全部经过抽象**: IChatClient / IEmbeddingGenerator, 不直接依赖任何 Provider SDK
- **配置驱动**: Agent 定义、模型选择、检索策略、审计模式等均为可配置项, 不硬编码
- **Chat 模型与 Embedding 模型独立配置**: Chat 随时换不影响已存数据, Embedding 项目级绑定不可随意换
- **Orchestrator 是确定性代码**: 不做语义判断, 只做编排, 语义判断全部交给各 Agent
- **事件溯源**: 所有状态变更记录为事件, 可重放可回滚
