using System.ComponentModel;

namespace DirectorPrompt.Domain.Enums;

public enum AuditDimension
{
    [Description("设定一致性")]
    Setting,

    [Description("状态一致性")]
    State,

    [Description("人物一致性")]
    Character,

    [Description("时间一致性")]
    Time,

    [Description("记忆一致性")]
    Memory
}
