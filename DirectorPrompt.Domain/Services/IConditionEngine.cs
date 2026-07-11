namespace DirectorPrompt.Domain.Services;

public interface IConditionEngine
{
    bool Evaluate(string expression, string currentValue);
}
