using TasklyDev.Windows.Environment;
using Xunit;

namespace TasklyDev.Tests;

public sealed class EnvironmentVariableServiceTests
{
    [Theory]
    [InlineData(EnvironmentVariableScope.Process)]
    [InlineData(EnvironmentVariableScope.User)]
    [InlineData(EnvironmentVariableScope.Machine)]
    public void ReadsRequestedScopeAsSortedEntries(EnvironmentVariableScope scope)
    {
        var variables = new EnvironmentVariableService().GetVariables(scope);

        Assert.NotNull(variables);
        Assert.Equal(
            variables.OrderBy(variable => variable.Name, StringComparer.OrdinalIgnoreCase).Select(variable => variable.Name),
            variables.Select(variable => variable.Name));
        Assert.All(variables, variable =>
        {
            Assert.False(string.IsNullOrWhiteSpace(variable.Name));
            Assert.NotNull(variable.Value);
        });
    }

    [Fact]
    public void RejectsUnknownScope()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new EnvironmentVariableService().GetVariables((EnvironmentVariableScope)999));
    }
}
