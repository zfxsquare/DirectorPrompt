namespace DirectorPrompt.Agents;

public record ToolExecutionContext
(
    long  ProjectID,
    long  SessionID,
    long? SceneID,
    long  TimelinePosition,
    long  RoundID
);
