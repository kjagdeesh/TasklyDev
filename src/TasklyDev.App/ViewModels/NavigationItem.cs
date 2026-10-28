using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TasklyDev.ViewModels;

/// <summary>Represents a sidebar destination and its active navigation state.</summary>
public sealed class NavigationItem(string title, string route, string glyph) : INotifyPropertyChanged
{
    private bool _isActive;

    public string Title { get; } = title;
    public string Route { get; } = route;
    public string Glyph { get; } = glyph;

    public bool IsActive
    {
        get => _isActive;
        set
        {
            if (_isActive == value)
            {
                return;
            }

            _isActive = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
