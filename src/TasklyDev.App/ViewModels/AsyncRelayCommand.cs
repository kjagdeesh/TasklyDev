using System.Windows.Input;

namespace TasklyDev.ViewModels;

/// <summary>Adapts an asynchronous operation to <see cref="ICommand"/> and prevents concurrent execution.</summary>
public sealed class AsyncRelayCommand(Func<Task> execute) : ICommand
{
    private bool _isRunning;

    public event EventHandler? CanExecuteChanged;

    /// <summary>Gets whether the command is currently available for execution.</summary>
    public bool CanExecute(object? parameter) => !_isRunning;

    /// <summary>Runs the asynchronous operation and raises availability-change notifications.</summary>
    public async void Execute(object? parameter)
    {
        if (_isRunning)
        {
            return;
        }

        _isRunning = true;
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        try
        {
            await execute();
        }
        finally
        {
            _isRunning = false;
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
