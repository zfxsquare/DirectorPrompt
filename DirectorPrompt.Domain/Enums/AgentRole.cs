using System.ComponentModel;

namespace DirectorPrompt.Domain.Enums;

public enum AgentRole
{
    [Description("叙述者")]
    Narrator,

    [Description("知识检索")]
    Knowledge,

    [Description("记忆管理")]
    Memory,

    [Description("状态提取")]
    State,

    [Description("审计")]
    Audit,

    [Description("场景管理")]
    Scene
}
