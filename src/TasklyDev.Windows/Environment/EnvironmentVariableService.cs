using System.Collections;
using System.Security;

namespace TasklyDev.Windows.Environment;

public sealed class EnvironmentVariableService : IEnvironmentVariableService
{
    public IReadOnlyList<EnvironmentVariableEntry> GetVariables(EnvironmentVariableScope scope)
    {
        var target = scope switch
        {
            EnvironmentVariableScope.Process => System.EnvironmentVariableTarget.Process,
            EnvironmentVariableScope.User => System.EnvironmentVariableTarget.User,
            EnvironmentVariableScope.Machine => System.EnvironmentVariableTarget.Machine,
            _ => throw new ArgumentOutOfRangeException(nameof(scope))
        };

        var variables = System.Environment.GetEnvironmentVariables(target);
        var result = new List<EnvironmentVariableEntry>(variables.Count);
        foreach (DictionaryEntry item in variables)
        {
            if (item.Key is string name)
            {
                result.Add(new EnvironmentVariableEntry(name, item.Value?.ToString() ?? string.Empty));
            }
        }

        return result.OrderBy(variable => variable.Name, StringComparer.OrdinalIgnoreCase).ToArray();
    }
}
