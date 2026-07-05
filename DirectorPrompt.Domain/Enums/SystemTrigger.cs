using System.ComponentModel;

namespace DirectorPrompt.Domain.Enums;

public enum SystemTrigger
{
    [Description("场景切换时")]
    SceneChange,

    [Description("每轮结束时")]
    RoundEnd,

    [Description("自定义")]
    Custom
}
