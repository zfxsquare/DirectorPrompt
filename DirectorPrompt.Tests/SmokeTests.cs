using DirectorPrompt.Agents;
using DirectorPrompt.Domain;
using DirectorPrompt.Infrastructure;
using Xunit;

namespace DirectorPrompt.Tests;

public class SmokeTests
{
    [Fact]
    public void DomainLayerExists() =>
        Assert.NotNull(typeof(DomainLayer));

    [Fact]
    public void AgentLayerExists() =>
        Assert.NotNull(typeof(AgentLayer));

    [Fact]
    public void InfrastructureLayerExists() =>
        Assert.NotNull(typeof(InfrastructureLayer));
}
