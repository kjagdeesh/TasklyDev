using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace TasklyDev.ViewModels;

/// <summary>Adapts an asynchronous operation to <see cref="ICommand"/> and prevents concurrent execution.</summary>
public sealed class AsyncRelayCommand(Func<Task> execute) : ICommand
{
    private readonly Dispatcher? _dispatcher = Application.Current?.Dispatcher;
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
        RaiseCanExecuteChanged();
        try
        {
            await execute();
        }
        finally
        {
            _isRunning = false;
            RaiseCanExecuteChanged();
        }
    }

    private void RaiseCanExecuteChanged()
    {
        if (_dispatcher is null || _dispatcher.CheckAccess())
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
            return;
        }

        if (!_dispatcher.HasShutdownStarted && !_dispatcher.HasShutdownFinished)
        {
            _dispatcher.BeginInvoke(
                DispatcherPriority.DataBind,
                new Action(() => CanExecuteChanged?.Invoke(this, EventArgs.Empty)));
        }
    }
}
