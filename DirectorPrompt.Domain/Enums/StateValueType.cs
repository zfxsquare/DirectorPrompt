using System.ComponentModel;

namespace DirectorPrompt.Domain.Enums;

public enum StateValueType
{
    [Description("数值 (连续数值, 如金钱、评价)")]
    Numeric,

    [Description("枚举 (离散选项, 如天气、季节)")]
    Enum,

    [Description("复合 (条目列表, 如任务、库存)")]
    Composite
}
