namespace TasklyDev.Windows.Environment;

/// <summary>Identifies the Windows environment-variable store to read.</summary>
public enum EnvironmentVariableScope
{
    /// <summary>Variables inherited by the current process.</summary>
    Process,
    /// <summary>Variables configured for the current Windows user.</summary>
    User,
    /// <summary>Variables configured for the local machine.</summary>
    Machine
}
