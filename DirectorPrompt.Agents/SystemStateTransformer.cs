using DirectorPrompt.Domain.Configurations;
using DirectorPrompt.Domain.Enums;
using DirectorPrompt.Domain.Models;
using DirectorPrompt.Domain.Repositories;
using DirectorPrompt.Domain.Services;
using Serilog;

namespace DirectorPrompt.Agents;

public sealed class SystemStateTransformer
(
    IStateRepository     stateRepository,
    ICharacterRepository characterRepository,
    IConditionEngine     conditionEngine
) : ISystemStateTransformer
{
    public async Task ExecuteAsync
    (
        long              projectID,
        long              sessionID,
        long?             sceneID,
        long              roundID,
        SystemTrigger     trigger,
        CancellationToken cancellationToken = default
    )
    {
        Log.Information
        (
            "系统状态变换开始: project={ProjectID}, session={SessionID}, trigger={Trigger}",
            projectID,
            sessionID,
            trigger
        );

        var attributes  = await stateRepository.GetAttributesAsync(projectID, null, cancellationToken);
        var systemAttrs = attributes.Where(a => a.Driver == Driver.System).ToList();

        if (systemAttrs.Count == 0)
        {
            Log.Debug("无 system 驱动的状态属性, 跳过");
            return;
        }

        var globalStateValues = await BuildGlobalStateContextAsync(attributes, sessionID, cancellationToken);
        var attrNameCache     = attributes.ToDictionary(a => a.ID, a => a.Name);

        foreach (var attr in systemAttrs)
        {
            if (attr.Scope == StateScope.Global)
                await TransformGlobalAttributeAsync(attr, sessionID, sceneID, roundID, trigger, globalStateValues, cancellationToken);
        }

        if (sceneID is not null)
        {
            var sceneCharacters = await characterRepository.GetBySceneAsync(sceneID.Value, cancellationToken);

            foreach (var attr in systemAttrs)
            {
                if (attr.Scope == StateScope.Category)
                {
                    await TransformCategoryAttributeAsync
                        (attr, sceneCharacters, sessionID, sceneID.Value, roundID, trigger, attrNameCache, globalStateValues, cancellationToken);
                }
            }
        }

        Log.Information("系统状态变换完成");
    }

    private async Task TransformGlobalAttributeAsync
    (
        StateAttribute             attr,
        long                       sessionID,
        long?                      sceneID,
        long                       roundID,
        SystemTrigger              trigger,
        Dictionary<string, string> globalStateValues,
        CancellationToken          cancellationToken
    )
    {
        if (attr.ValueType == StateValueType.Enum)
        {
            var value        = await stateRepository.GetStateValueAsync(attr.ID, sessionID, cancellationToken);
            var currentValue = value?.Value ?? string.Empty;

            await TransformEnumAttributeAsync
            (
                attr,
                sessionID,
                sceneID,
                roundID,
                trigger,
                currentValue,
                globalStateValues,
                null,
                cancellationToken
            );
        }
        else if (attr.ValueType == StateValueType.Composite)
        {
            await TransformCompositeAttributeAsync
            (
                attr,
                sessionID,
                trigger,
                globalStateValues,
                cancellationToken
            );
        }
    }

    private async Task TransformCategoryAttributeAsync
    (
        StateAttribute             attr,
        IReadOnlyList<Character>   characters,
        long                       sessionID,
        long                       sceneID,
        long                       roundID,
        SystemTrigger              trigger,
        Dictionary<long, string>   attrNameCache,
        Dictionary<string, string> globalStateValues,
        CancellationToken          cancellationToken
    )
    {
        foreach (var character in characters)
        {
            var charValues = await characterRepository.GetCharacterStateValuesAsync(character.ID, cancellationToken);
            var charContext = charValues.ToDictionary
            (
                v => attrNameCache.TryGetValue(v.AttributeID, out var name) ?
                         name :
                         v.AttributeID.ToString(),
                v => v.Value
            );

            if (attr.ValueType == StateValueType.Enum)
            {
                var currentValue = charValues.FirstOrDefault(v => v.AttributeID == attr.ID)?.Value ?? string.Empty;

                await TransformEnumAttributeAsync
                (
                    attr,
                    sessionID,
                    sceneID,
                    roundID,
                    trigger,
                    currentValue,
                    charContext,
                    character.ID,
                    cancellationToken
                );
            }
            else if (attr.ValueType == StateValueType.Composite)
            {
                await TransformCompositeAttributeAsync
                (
                    attr,
                    sessionID,
                    trigger,
                    globalStateValues,
                    cancellationToken
                );
            }
        }
    }

    private async Task TransformEnumAttributeAsync
    (
        StateAttribute             attr,
        long                       sessionID,
        long?                      sceneID,
        long                       roundID,
        SystemTrigger              trigger,
        string                     currentValue,
        Dictionary<string, string> stateValues,
        long?                      characterID,
        CancellationToken          cancellationToken
    )
    {
        var config = AttributeConfigSerializer.Deserialize<EnumAttributeConfig>(attr.Config);

        if (config is null)
            return;

        if (!IsTriggerMatch(ParseTrigger(config.Trigger), trigger))
            return;

        if (string.IsNullOrEmpty(currentValue))
            currentValue = config.Options.FirstOrDefault() ?? string.Empty;

        var newValue = ResolveEnumTransition(currentValue, config, stateValues);

        if (newValue == currentValue)
            return;

        if (characterID is not null)
            await characterRepository.SetCharacterStateValueAsync(characterID.Value, attr.ID, newValue, cancellationToken);
        else
        {
            await stateRepository.SetStateValueAsync
            (
                attr.ID,
                sessionID,
                newValue,
                StateChangeSource.System,
                $"system 变换: {currentValue} → {newValue}",
                sceneID ?? 0,
                roundID,
                cancellationToken
            );
        }

        Log.Information
        (
            "状态变换: {AttrName} {OldValue} → {NewValue} (character={CharacterID})",
            attr.Name,
            currentValue,
            newValue,
            characterID
        );
    }

    private async Task TransformCompositeAttributeAsync
    (
        StateAttribute             attr,
        long                       sessionID,
        SystemTrigger              trigger,
        Dictionary<string, string> globalStateValues,
        CancellationToken          cancellationToken
    )
    {
        var config = AttributeConfigSerializer.Deserialize<CompositeAttributeConfig>(attr.Config);

        if (config is null)
            return;

        if (config.RegenerateTrigger is not null && IsTriggerMatch(ParseTrigger(config.RegenerateTrigger), trigger))
        {
            var shouldRegenerate = true;

            if (!string.IsNullOrWhiteSpace(config.RegenerateCondition))
            {
                var context = new ConditionContext(globalStateValues);
                shouldRegenerate = conditionEngine.Evaluate(config.RegenerateCondition, context);
            }

            if (shouldRegenerate)
            {
                Log.Information
                (
                    "composite 属性 {AttrName} 触发重新生成 (AI 生成尚未实现, 跳过)",
                    attr.Name
                );
            }
        }

        var items = await stateRepository.GetCompositeItemsAsync(attr.ID, sessionID, cancellationToken);

        foreach (var item in items)
        {
            if (item.Status != CompositeItemStatus.Active)
                continue;

            if (item.Current >= item.Target && item.Target > 0)
                await stateRepository.UpdateCompositeItemAsync(item.ID, null, item.Target, "system 自动完成", cancellationToken);
        }
    }

    private string ResolveEnumTransition
    (
        string                     currentValue,
        EnumAttributeConfig        config,
        Dictionary<string, string> stateValues
    )
    {
        if (config.Conditions.Count > 0)
        {
            var context = new ConditionContext(stateValues);

            foreach (var cond in config.Conditions)
            {
                if (conditionEngine.Evaluate(cond.When, context))
                    return PickWeighted(cond.Transition);
            }
        }

        if (config.TransitionRules.TryGetValue(currentValue, out var rules))
            return PickWeighted(rules);

        return currentValue;
    }

    private static string PickWeighted(Dictionary<string, float> weights)
    {
        var total = weights.Values.Sum();

        if (total <= 0)
            return weights.Keys.FirstOrDefault() ?? string.Empty;

        var roll       = (float)Random.Shared.NextDouble() * total;
        var cumulative = 0f;

        foreach (var (key, weight) in weights)
        {
            cumulative += weight;

            if (roll <= cumulative)
                return key;
        }

        return weights.Keys.Last();
    }

    private async Task<Dictionary<string, string>> BuildGlobalStateContextAsync
    (
        IReadOnlyList<StateAttribute> allAttributes,
        long                          sessionID,
        CancellationToken             cancellationToken
    )
    {
        var result = new Dictionary<string, string>();

        foreach (var attr in allAttributes.Where(a => a.Scope == StateScope.Global))
        {
            var value = await stateRepository.GetStateValueAsync(attr.ID, sessionID, cancellationToken);
            result[attr.Name] = value?.Value ?? string.Empty;
        }

        return result;
    }

    private static bool IsTriggerMatch(SystemTrigger configTrigger, SystemTrigger actualTrigger) =>
        configTrigger == actualTrigger;

    private static SystemTrigger ParseTrigger(string? value) =>
        value switch
        {
            "scene_change" => SystemTrigger.SceneChange,
            "round_end"    => SystemTrigger.RoundEnd,
            "custom"       => SystemTrigger.Custom,
            _              => SystemTrigger.RoundEnd
        };
}
