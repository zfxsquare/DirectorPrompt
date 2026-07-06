using System.Text;
using DirectorPrompt.Agents.Prompts;
using DirectorPrompt.Agents.Tools;
using DirectorPrompt.Domain.Configurations;
using DirectorPrompt.Domain.Enums;
using DirectorPrompt.Domain.Repositories;
using Microsoft.Extensions.AI;
using Serilog;

namespace DirectorPrompt.Agents.Pipeline;

public sealed class PostProcessingStage
{
    private readonly IChatClientFactory   chatClientFactory;
    private readonly MemoryTools          memoryTools;
    private readonly StateTools           stateTools;
    private readonly CharacterTools       characterTools;
    private readonly OrchestratorConfig   orchestratorConfig;
    private readonly IStateRepository     stateRepository;
    private readonly ICharacterRepository characterRepository;
    private readonly ISceneRepository     sceneRepository;

    public PostProcessingStage
    (
        IChatClientFactory   chatClientFactory,
        MemoryTools          memoryTools,
        StateTools           stateTools,
        CharacterTools       characterTools,
        OrchestratorConfig   orchestratorConfig,
        IStateRepository     stateRepository,
        ICharacterRepository characterRepository,
        ISceneRepository     sceneRepository
    )
    {
        this.chatClientFactory   = chatClientFactory;
        this.memoryTools         = memoryTools;
        this.stateTools          = stateTools;
        this.characterTools      = characterTools;
        this.orchestratorConfig  = orchestratorConfig;
        this.stateRepository     = stateRepository;
        this.characterRepository = characterRepository;
        this.sceneRepository     = sceneRepository;
    }

    public async Task ExecuteAsync(PipelineContext context, CancellationToken cancellationToken = default)
    {
        var memoryAgent = orchestratorConfig.Agents.FirstOrDefault(a => a.Role == AgentRole.Memory);

        if (memoryAgent is null || !memoryAgent.Enabled)
        {
            Log.Debug("PostProcessingStage: Memory Agent 未启用, 跳过");
            return;
        }

        Log.Information
        (
            "PostProcessingStage 开始: 模型={Model}",
            memoryAgent.ModelConfig.ModelName
        );

        Log.Debug("后处理输入 (叙事文本):\n{Narrative}", context.NarrativeOutput ?? "(空)");

        var client      = chatClientFactory.Create(memoryAgent.ModelConfig);
        var toolContext = context.ToolContext;

        var tools = new List<AIFunction>();
        tools.AddRange(memoryTools.Create(toolContext));
        tools.AddRange(stateTools.Create(toolContext));
        tools.AddRange(characterTools.Create(toolContext));

        var agentContext = await BuildAgentContextAsync(toolContext, cancellationToken);

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, MemorySubAgentPrompt.Update),
            new(ChatRole.User, $"{agentContext}\n\n---\n\n## 叙事文本\n{context.NarrativeOutput ?? string.Empty}")
        };

        var options = new ChatOptions
        {
            Temperature = memoryAgent.Temperature,
            ModelId     = memoryAgent.ModelConfig.ModelName,
            Tools       = [.. tools]
        };

        await client.GetResponseAsync(messages, options, cancellationToken);

        Log.Information("PostProcessingStage 完成: Memory Agent 已处理叙事文本");
    }

    private async Task<string> BuildAgentContextAsync(ToolExecutionContext context, CancellationToken cancellationToken)
    {
        var sb = new StringBuilder();

        if (context.SceneID is not null)
        {
            var scene = await sceneRepository.GetByIDAsync(context.SceneID.Value, cancellationToken);

            if (scene is not null)
            {
                sb.AppendLine("## 当前场景");
                sb.AppendLine($"- ID: {scene.ID}");
                sb.AppendLine($"- 时间标签: {scene.TimeLabel}");
                sb.AppendLine($"- 状态: {scene.Status}");
                sb.AppendLine();
            }
        }

        var attributes = await stateRepository.GetAttributesAsync(context.ProjectID, StateScope.Global, cancellationToken);

        if (attributes.Count > 0)
        {
            sb.AppendLine("## 可用状态属性 (调用工具时使用 Name 字段的值)");
            sb.AppendLine("| Name | 显示名 | 当前值 | 类型 |");
            sb.AppendLine("|------|--------|--------|------|");

            foreach (var attr in attributes)
            {
                var value = await stateRepository.GetStateValueAsync(attr.ID, context.SessionID, cancellationToken);
                sb.AppendLine($"| {attr.Name} | {attr.DisplayName} | {value?.Value ?? "未设置"} | {attr.ValueType} |");
            }

            sb.AppendLine();
        }

        var characters = await characterRepository.GetBySessionAsync(context.SessionID, cancellationToken);

        if (characters.Count > 0)
        {
            sb.AppendLine("## 已有人物 (不要重复添加)");
            foreach (var c in characters)
                sb.AppendLine($"- {c.Name} ({c.Status}): {c.Description}");

            sb.AppendLine();
        }

        return sb.ToString();
    }
}
