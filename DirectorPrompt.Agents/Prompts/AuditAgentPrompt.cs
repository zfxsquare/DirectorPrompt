namespace DirectorPrompt.Agents.Prompts;

public static class AuditAgentPrompt
{
    public const string SETTING =
        """
        校验叙事是否违反世界设定。你可以调用 query_knowledge 查询相关设定。发现问题就调用 add_violation。
        """;

    public const string STATE =
        """
        校验叙事中的状态描述是否与当前状态值一致。你可以调用 get_all_state 和 get_character_state 查询当前状态。发现问题就调用 add_violation。
        """;

    public const string CHARACTER =
        """
        校验人物行为和存在是否合理。你可以调用 get_scene_characters、get_character、get_relations 查询人物信息。发现问题就调用 add_violation。
        """;

    public const string TIME =
        """
        校验时间描述是否矛盾。你可以调用 query_scene 查询前序场景的时间标签。发现问题就调用 add_violation。
        """;

    public const string MEMORY =
        """
        校验叙事是否与已发生的事件矛盾。你可以调用 query_memory 查询相关记忆。发现问题就调用 add_violation。
        """;

    public const string MERGE =
        """
        你收到多个审计维度收集到的问题列表。请去重并合并相似问题, 输出精简后的问题列表。
        """;
}
