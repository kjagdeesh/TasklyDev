namespace TasklyDev.Windows.Environment;

public interface IEnvironmentVariableService
{
    IReadOnlyList<EnvironmentVariableEntry> GetVariables(EnvironmentVariableScope scope);
}
