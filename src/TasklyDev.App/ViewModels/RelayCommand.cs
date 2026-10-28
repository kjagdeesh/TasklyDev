using System;
using System.Windows.Input;

namespace TasklyDev.ViewModels;

/// <summary>Adapts a synchronous delegate to the WPF command interface.</summary>
public sealed class RelayCommand(Action<object?> execute, Predicate<object?>? canExecute = null) : ICommand
{
    public event EventHandler? CanExecuteChanged;

    /// <summary>Determines whether the command can run for the supplied parameter.</summary>
    public bool CanExecute(object? parameter) => canExecute?.Invoke(parameter) ?? true;

    /// <summary>Executes the command delegate.</summary>
    public void Execute(object? parameter) => execute(parameter);

    /// <summary>Notifies command sources to reevaluate whether the command can run.</summary>
    public void NotifyCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
