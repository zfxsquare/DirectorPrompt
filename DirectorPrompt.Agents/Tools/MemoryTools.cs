using System.Text.Json;
using DirectorPrompt.Domain.Models;
using DirectorPrompt.Domain.Repositories;
using DirectorPrompt.Domain.Services;
using Microsoft.Extensions.AI;

namespace DirectorPrompt.Agents.Tools;

public sealed class MemoryTools
{
    private readonly IMemoryRepository memoryRepository;
    private readonly IEmbeddingService embeddingService;

    public MemoryTools(IMemoryRepository memoryRepository, IEmbeddingService embeddingService)
    {
        this.memoryRepository = memoryRepository;
        this.embeddingService = embeddingService;
    }

    public IList<AIFunction> Create(ToolExecutionContext context) =>
    [
        AIFunctionFactory.Create
        (
            (string query, string? tags, int? topK) => QueryMemoryAsync(context, query, tags, topK),
            "query_memory",
            "语义检索记忆条目。query: 检索内容; tags: 可选, 按标签过滤 (逗号分隔); topK: 返回条数, 默认 10"
        ),
        AIFunctionFactory.Create
        (
            (long sceneID, string content, string tags) =>
                CreateMemoryAsync(context, sceneID, content, tags),
            "create_memory",
            "创建新记忆。sceneID: 归属场景 ID; content: 记忆正文; tags: 标签 (逗号分隔)"
        ),
        AIFunctionFactory.Create
        (
            (long memoryID, string content, string? tags) =>
                UpdateMemoryAsync(context, memoryID, content, tags),
            "update_memory",
            "改写已有记忆。memoryID: 记忆 ID; content: 新内容; tags: 可选, 新标签 (逗号分隔)"
        ),
        AIFunctionFactory.Create
        (
            (string memoryIDs, string content, string tags) =>
                MergeMemoriesAsync(context, memoryIDs, content, tags),
            "merge_memories",
            "合并多条记忆为一条。memoryIDs: 要合并的记忆 ID 列表 (逗号分隔); content: 合并后的内容; tags: 标签 (逗号分隔)"
        )
    ];

    private async Task<string> QueryMemoryAsync
    (
        ToolExecutionContext context,
        string               query,
        string?              tags,
        int?                 topK
    )
    {
        var memories = await memoryRepository.GetBySessionAsync(context.SessionID, context.TimelinePosition);

        if (memories.Count == 0)
            return JsonSerializer.Serialize(Array.Empty<object>());

        var limit = topK ?? 10;

        var filtered = memories.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(tags))
        {
            var tagList = tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            filtered = filtered.Where(m => m.Tags.Any(t => tagList.Contains(t)));
        }

        var result = filtered
                     .Take(limit)
                     .Select
                     (m => new
                         {
                             id      = m.ID,
                             content = m.Content,
                             tags    = m.Tags,
                             sceneID = m.SceneID
                         }
                     );

        return JsonSerializer.Serialize(result);
    }

    private async Task<string> CreateMemoryAsync
    (
        ToolExecutionContext context,
        long                 sceneID,
        string               content,
        string               tags
    )
    {
        var tagList = ParseTags(tags);

        var entry = new MemoryEntry
        {
            ProjectID   = context.ProjectID,
            SessionID   = context.SessionID,
            SceneID     = sceneID,
            TimelinePos = context.TimelinePosition,
            Content     = content,
            Tags        = tagList
        };

        var created = await memoryRepository.CreateAsync(entry);

        return JsonSerializer.Serialize(new { memoryID = created.ID });
    }

    private async Task<string> UpdateMemoryAsync
    (
        ToolExecutionContext context,
        long                 memoryID,
        string               content,
        string?              tags
    )
    {
        var existing = await memoryRepository.GetByIDAsync(memoryID);

        if (existing is null)
            return JsonSerializer.Serialize(new { error = $"记忆 {memoryID} 不存在" });

        var updated = existing with
        {
            Content = content,
            Tags = string.IsNullOrWhiteSpace(tags) ?
                       existing.Tags :
                       ParseTags(tags)
        };

        await memoryRepository.UpdateAsync(updated);

        return JsonSerializer.Serialize(new { memoryID, success = true });
    }

    private async Task<string> MergeMemoriesAsync
    (
        ToolExecutionContext context,
        string               memoryIDs,
        string               content,
        string               tags
    )
    {
        var idList = memoryIDs
                     .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                     .Select(long.Parse)
                     .ToList();

        var tagList = ParseTags(tags);
        var merged  = await memoryRepository.MergeAsync(idList, context.SceneID ?? 0, content, tagList);

        return JsonSerializer.Serialize(new { memoryID = merged.ID });
    }

    private static string[] ParseTags(string tags) =>
        tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
