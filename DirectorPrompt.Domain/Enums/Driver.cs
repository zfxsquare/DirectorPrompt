using System.ComponentModel;

namespace DirectorPrompt.Domain.Enums;

public enum Driver
{
    [Description("叙事驱动 (AI 从叙事中提取变更)")]
    Narrative,

    [Description("系统驱动 (按规则自动变换)")]
    System
}
