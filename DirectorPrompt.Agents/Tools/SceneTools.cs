using System.Text.Json;
using DirectorPrompt.Domain.Enums;
using DirectorPrompt.Domain.Models;
using DirectorPrompt.Domain.Repositories;
using DirectorPrompt.Domain.Services;
using Microsoft.Extensions.AI;

namespace DirectorPrompt.Agents.Tools;

public sealed class SceneTools
{
    private readonly ISceneRepository    sceneRepository;
    private readonly ITimelineCalculator timelineCalculator;

    public SceneTools(ISceneRepository sceneRepository, ITimelineCalculator timelineCalculator)
    {
        this.sceneRepository    = sceneRepository;
        this.timelineCalculator = timelineCalculator;
    }

    public IList<AIFunction> Create(ToolExecutionContext context) =>
    [
        AIFunctionFactory.Create
        (
            () => QuerySceneAsync(context),
            "query_scene",
            "查询当前对话的所有场景列表, 返回每个场景的 ID、timelinePosition、timeLabel、status"
        ),
        AIFunctionFactory.Create
        (
            (long? afterSceneID, long? beforeSceneID, string timeLabel) =>
                CreateSceneAsync(context, afterSceneID, beforeSceneID, timeLabel),
            "create_scene",
            "创建新场景。afterSceneID: 新场景在时间轴上位于此场景之后; beforeSceneID: 新场景在时间轴上位于此场景之前; 至少填一个, 都填表示插入两者之间; timeLabel: 语义时间标签"
        )
    ];

    private async Task<string> QuerySceneAsync(ToolExecutionContext context)
    {
        var scenes = await sceneRepository.GetOrderedByTimelineAsync(context.SessionID);
        var result = scenes.Select
        (s => new
            {
                id               = s.ID,
                timelinePosition = s.TimelinePosition,
                timeLabel        = s.TimeLabel,
                status           = s.Status.ToString()
            }
        );

        return JsonSerializer.Serialize(result);
    }

    private async Task<string> CreateSceneAsync
    (
        ToolExecutionContext context,
        long?                afterSceneID,
        long?                beforeSceneID,
        string               timeLabel
    )
    {
        if (string.IsNullOrWhiteSpace(timeLabel))
            return JsonSerializer.Serialize(new { error = "timeLabel 不能为空" });

        var existingScenes = await sceneRepository.GetOrderedByTimelineAsync(context.SessionID);

        long position;

        try
        {
            position = timelineCalculator.CalculatePosition(afterSceneID, beforeSceneID, existingScenes);
        }
        catch (ArgumentException ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message });
        }

        var scene = new Scene
        {
            ProjectID        = context.ProjectID,
            SessionID        = context.SessionID,
            TimelinePosition = position,
            TimeLabel        = timeLabel,
            Status           = SceneStatus.Active
        };

        var created = await sceneRepository.CreateAsync(scene);

        return JsonSerializer.Serialize(new { sceneID = created.ID, timelinePosition = position });
    }
}
