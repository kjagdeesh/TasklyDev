namespace TasklyDev.Windows.Environment;

/// <summary>Reads environment variables from a selected Windows scope.</summary>
public interface IEnvironmentVariableService
{
    /// <summary>Returns the variables available at the requested scope.</summary>
    /// <param name="scope">Process, current-user, or machine scope to read.</param>
    /// <returns>Variables sorted by name without modifying the environment.</returns>
    IReadOnlyList<EnvironmentVariableEntry> GetVariables(EnvironmentVariableScope scope);
}
