using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using TasklyDev.Core.Conversion;
using TasklyDev.Core.Colors;
using TasklyDev.Core.Encoding;
using TasklyDev.Core.Identifiers;
using TasklyDev.Core.Hashing;
using TasklyDev.Core.RegularExpressions;
using TasklyDev.Core.Time;
using TasklyDev.Models;
using TasklyDev.Services;
using TasklyDev.Windows.Environment;
using TasklyDev.Windows.Processes;

namespace TasklyDev.ViewModels;

/// <summary>Coordinates shell navigation, tool search, and the application's screen view models.</summary>
public sealed class MainWindowViewModel : INotifyPropertyChanged
{
    private static readonly ToolDefinition[] Tools =
    [
        new("Base64 Encoder & Decoder", "Encode UTF-8 text or decode Base64 with clear validation.", "base64", "B64", "Developer Tools"),
        new("Number Base Converter", "Convert arbitrary-precision integers across bases 2 through 36.", "number-base", "2x", "Developer Tools"),
        new("Unix Timestamp Converter", "Convert explicit seconds or milliseconds to UTC and local time.", "timestamp", "UTC", "Developer Tools"),
        new("GUID Generator", "Generate RFC-compatible GUIDs in common .NET formats.", "guid", "ID", "Developer Tools"),
        new("Hash Calculator", "Calculate MD5, SHA-256, or SHA-512 digests for text or files.", "hash", "#", "Developer Tools"),
        new("Color Converter", "Convert HEX, RGB, and HSL values or choose a color from the Windows color dialog.", "color", "RGB", "Developer Tools"),
        new("Regex Tester", "Test .NET regular expressions with captures and a finite timeout.", "regex", ".*", "Developer Tools"),
        new("Process Lookup", "Search a read-only snapshot of running Windows processes.", "process", "PID", "System Diagnostics"),
        new("Environment Variables", "Browse process, user, and machine variables without editing them.", "environment", "$", "System Diagnostics")
    ];

    private readonly ObservableCollection<NavigationItem> _developerNavigation = [];
    private readonly ObservableCollection<NavigationItem> _systemNavigation = [];
    private readonly ObservableCollection<ToolDefinition> _developerTools = [];
    private readonly ObservableCollection<ToolDefinition> _systemTools = [];
    private string _searchText = string.Empty;
    private string _currentRoute = "home";
    private string _currentPageTitle = "Utilities Workbench";
    private string _currentPageSubtitle = "A focused set of local tools for everyday development tasks.";
    private ToolDefinition? _currentTool;

    /// <summary>Creates the shell view models and navigates to the initial Overview screen.</summary>
    public MainWindowViewModel(IClipboardService? clipboard = null, IThemeService? themeService = null)
    {
        var clipboardService = clipboard ?? new WpfClipboardService();
        OpenRouteCommand = new RelayCommand(parameter =>
        {
            if (parameter is string route)
            {
                Navigate(route);
            }
        });
        ClearSearchCommand = new RelayCommand(_ => SearchText = string.Empty);
        OpenToolCommand = new RelayCommand(parameter =>
        {
            if (parameter is string route)
            {
                Navigate(route);
            }
        });

        OverviewNavigation = new NavigationItem("Overview", "home", "⌂");
        SettingsNavigation = new NavigationItem("Preferences", "settings", "⚙");
        Base64ViewModel = new Base64ViewModel(new Base64Service(), clipboardService);
        NumberBaseViewModel = new NumberBaseViewModel(new NumberBaseService(), clipboardService);
        UnixTimestampViewModel = new UnixTimestampViewModel(new TimestampService(), clipboardService);
        GuidGeneratorViewModel = new GuidGeneratorViewModel(new GuidService(), clipboardService);
        HashCalculatorViewModel = new HashCalculatorViewModel(
            new HashService(),
            clipboardService,
            new WpfFilePickerService());
        ColorConverterViewModel = new ColorConverterViewModel(
            new ColorService(),
            clipboardService,
            new WpfColorPickerService());
        RegexTesterViewModel = new RegexTesterViewModel(new RegexTesterService());
        ProcessLookupViewModel = new ProcessLookupViewModel(new ProcessSnapshotService());
        EnvironmentVariableViewModel = new EnvironmentVariableViewModel(
            new EnvironmentVariableService(),
            clipboardService);
        SettingsViewModel = new SettingsViewModel(themeService ?? new WpfThemeService());
        foreach (var tool in Tools)
        {
            var navigationItem = new NavigationItem(tool.Name, tool.Route, tool.Glyph);
            (tool.Group == "Developer Tools" ? _developerNavigation : _systemNavigation).Add(navigationItem);
        }

        Navigate("home");
        ApplyToolFilter();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public NavigationItem OverviewNavigation { get; }
    public Base64ViewModel Base64ViewModel { get; }
    public NumberBaseViewModel NumberBaseViewModel { get; }
    public UnixTimestampViewModel UnixTimestampViewModel { get; }
    public GuidGeneratorViewModel GuidGeneratorViewModel { get; }
    public HashCalculatorViewModel HashCalculatorViewModel { get; }
    public ColorConverterViewModel ColorConverterViewModel { get; }
    public RegexTesterViewModel RegexTesterViewModel { get; }
    public ProcessLookupViewModel ProcessLookupViewModel { get; }
    public EnvironmentVariableViewModel EnvironmentVariableViewModel { get; }
    public SettingsViewModel SettingsViewModel { get; }
    public NavigationItem SettingsNavigation { get; }
    public ObservableCollection<NavigationItem> DeveloperNavigation => _developerNavigation;
    public ObservableCollection<NavigationItem> SystemNavigation => _systemNavigation;
    public ObservableCollection<ToolDefinition> DeveloperTools => _developerTools;
    public ObservableCollection<ToolDefinition> SystemTools => _systemTools;
    public ICommand OpenRouteCommand { get; }
    public ICommand OpenToolCommand { get; }
    public ICommand ClearSearchCommand { get; }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (_searchText == value)
            {
                return;
            }

            _searchText = value;
            if (!string.IsNullOrWhiteSpace(value) && !IsHome)
            {
                Navigate("home");
            }

            OnPropertyChanged();
            OnPropertyChanged(nameof(HasSearchText));
            ApplyToolFilter();
        }
    }

    public bool HasSearchText => !string.IsNullOrWhiteSpace(SearchText);
    public bool HasNoSearchResults => HasSearchText && !HasSearchResults;
    public bool IsHome => _currentRoute == "home";
    public bool IsBase64 => _currentRoute == "base64";
    public bool IsNumberBase => _currentRoute == "number-base";
    public bool IsTimestamp => _currentRoute == "timestamp";
    public bool IsGuidGenerator => _currentRoute == "guid";
    public bool IsHashCalculator => _currentRoute == "hash";
    public bool IsColorConverter => _currentRoute == "color";
    public bool IsRegexTester => _currentRoute == "regex";
    public bool IsProcessLookup => _currentRoute == "process";
    public bool IsEnvironmentVariables => _currentRoute == "environment";
    public bool IsSettings => _currentRoute == "settings";
    public bool IsPlaceholder => !IsHome && !IsBase64 && !IsNumberBase && !IsTimestamp && !IsGuidGenerator && !IsHashCalculator && !IsColorConverter && !IsRegexTester && !IsProcessLookup && !IsEnvironmentVariables && !IsSettings;
    public bool HasDeveloperTools => _developerTools.Count > 0;
    public bool HasSystemTools => _systemTools.Count > 0;
    public bool HasSearchResults => HasDeveloperTools || HasSystemTools;

    public string CurrentPageTitle
    {
        get => _currentPageTitle;
        private set
        {
            if (_currentPageTitle == value)
            {
                return;
            }

            _currentPageTitle = value;
            OnPropertyChanged();
        }
    }

    public string CurrentPageSubtitle
    {
        get => _currentPageSubtitle;
        private set
        {
            if (_currentPageSubtitle == value)
            {
                return;
            }

            _currentPageSubtitle = value;
            OnPropertyChanged();
        }
    }

    public ToolDefinition? CurrentTool
    {
        get => _currentTool;
        private set
        {
            if (_currentTool == value)
            {
                return;
            }

            _currentTool = value;
            OnPropertyChanged();
        }
    }

    private void Navigate(string route)
    {
        _currentRoute = route;
        CurrentTool = Tools.FirstOrDefault(tool => tool.Route == route);

        foreach (var item in AllNavigationItems())
        {
            item.IsActive = item.Route == route;
        }

        if (route == "home")
        {
            CurrentPageTitle = "Utilities Workbench";
            CurrentPageSubtitle = "A focused set of local tools for everyday development tasks.";
        }
        else if (route == "settings")
        {
            CurrentPageTitle = "Preferences";
            CurrentPageSubtitle = "Appearance and information about TasklyDev.";
        }
        else if (CurrentTool is not null)
        {
            CurrentPageTitle = CurrentTool.Name;
            CurrentPageSubtitle = CurrentTool.Description;
        }
        else
        {
            return;
        }

        OnPropertyChanged(nameof(IsHome));
        OnPropertyChanged(nameof(IsBase64));
        OnPropertyChanged(nameof(IsNumberBase));
        OnPropertyChanged(nameof(IsTimestamp));
        OnPropertyChanged(nameof(IsGuidGenerator));
        OnPropertyChanged(nameof(IsHashCalculator));
        OnPropertyChanged(nameof(IsColorConverter));
        OnPropertyChanged(nameof(IsRegexTester));
        OnPropertyChanged(nameof(IsProcessLookup));
        OnPropertyChanged(nameof(IsEnvironmentVariables));
        OnPropertyChanged(nameof(IsSettings));
        OnPropertyChanged(nameof(IsPlaceholder));
    }

    private IEnumerable<NavigationItem> AllNavigationItems()
    {
        yield return OverviewNavigation;
        foreach (var item in _developerNavigation)
        {
            yield return item;
        }

        foreach (var item in _systemNavigation)
        {
            yield return item;
        }

        yield return SettingsNavigation;
    }

    private void ApplyToolFilter()
    {
        var term = SearchText.Trim();
        var matches = Tools.Where(tool =>
            term.Length == 0 ||
            tool.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
            tool.Description.Contains(term, StringComparison.OrdinalIgnoreCase));

        ReplaceItems(_developerTools, matches.Where(tool => tool.Group == "Developer Tools"));
        ReplaceItems(_systemTools, matches.Where(tool => tool.Group == "System Diagnostics"));
        OnPropertyChanged(nameof(HasDeveloperTools));
        OnPropertyChanged(nameof(HasSystemTools));
        OnPropertyChanged(nameof(HasSearchResults));
        OnPropertyChanged(nameof(HasNoSearchResults));
    }

    private static void ReplaceItems(ObservableCollection<ToolDefinition> destination, IEnumerable<ToolDefinition> values)
    {
        destination.Clear();
        foreach (var value in values)
        {
            destination.Add(value);
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
