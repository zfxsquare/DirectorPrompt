namespace DirectorPrompt.Domain.Enums;

public enum PipelineStageKind
{
    DirectiveProcessing,
    Retrieval,
    Generation,
    Audit,
    PostProcessing
}

public enum PipelineStageStatus
{
    Running,
    Complete
}
