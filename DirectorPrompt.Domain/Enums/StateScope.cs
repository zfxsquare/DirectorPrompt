using System.ComponentModel;

namespace DirectorPrompt.Domain.Enums;

public enum StateScope
{
    [Description("全局")]
    Global,

    [Description("人物分类")]
    Category
}
