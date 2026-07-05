using System.ComponentModel;

namespace DirectorPrompt.Domain.Enums;

public enum AuditMode
{
    [Description("阻断模式 (不通过则重生成)")]
    Blocking,

    [Description("标记模式 (放行带警告)")]
    Marking,

    [Description("关闭审计")]
    Disabled
}
