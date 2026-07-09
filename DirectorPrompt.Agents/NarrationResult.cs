namespace DirectorPrompt.Agents;

public record NarrationResult
(
    string Narrative,
    string Thinking,
    long   RoundID
);
