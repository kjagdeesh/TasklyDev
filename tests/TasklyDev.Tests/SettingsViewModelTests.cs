using TasklyDev.Services;
using TasklyDev.ViewModels;
using Xunit;

namespace TasklyDev.Tests;

public sealed class SettingsViewModelTests
{
    [Theory]
    [InlineData(ThemePreference.Light)]
    [InlineData(ThemePreference.Dark)]
    [InlineData(ThemePreference.System)]
    public void ThemeSelectionAppliesImmediately(ThemePreference preference)
    {
        var themeService = new FakeThemeService(preference == ThemePreference.System ? ThemePreference.Light : ThemePreference.System);
        var viewModel = new SettingsViewModel(themeService);

        viewModel.SelectedTheme = preference;

        Assert.Equal(preference, themeService.Preference);
        Assert.Equal(preference, viewModel.SelectedTheme);
        Assert.False(viewModel.IsError);
        Assert.Contains("session-only", viewModel.StatusMessage);
    }

    [Fact]
    public void SystemAppearanceChangesAreReflectedInSettingsStatus()
    {
        var themeService = new FakeThemeService();
        var viewModel = new SettingsViewModel(themeService);

        themeService.RaiseSystemThemeChanged();

        Assert.Contains("updated to match", viewModel.StatusMessage);
    }

    [Fact]
    public void SystemAppearanceReadFailureIsSurfaced()
    {
        var themeService = new FakeThemeService();
        var viewModel = new SettingsViewModel(themeService);

        themeService.RaiseSystemThemeReadFailed();

        Assert.True(viewModel.IsError);
        Assert.Contains("could not be read", viewModel.StatusMessage);
    }

    [Fact]
    public void ThemeFailureIsSurfacedAndSelectedValueIsReverted()
    {
        var themeService = new FakeThemeService
        {
            Failure = new InvalidOperationException("theme test failure")
        };
        var viewModel = new SettingsViewModel(themeService);

        viewModel.SelectedTheme = ThemePreference.Dark;

        Assert.Equal(ThemePreference.System, viewModel.SelectedTheme);
        Assert.True(viewModel.IsError);
        Assert.Contains("could not be applied", viewModel.StatusMessage);
    }

    [Fact]
    public void AboutProvidesVersionRuntimePrivacyAndLicenseInformation()
    {
        var viewModel = new SettingsViewModel(new FakeThemeService());

        Assert.Equal("TasklyDev", viewModel.ProductName);
        Assert.False(string.IsNullOrWhiteSpace(viewModel.Version));
        Assert.Contains(".NET", viewModel.RuntimeVersion);
        Assert.Contains("does not send telemetry", viewModel.PrivacyStatement);
        Assert.Contains("license notices", viewModel.LicenseStatement);
    }

    private sealed class FakeThemeService : IThemeService
    {
        public FakeThemeService(ThemePreference preference = ThemePreference.System)
        {
            Preference = preference;
        }

        public ThemePreference Preference { get; private set; }
        public Exception? Failure { get; init; }

        public event EventHandler? SystemThemeChanged;
        public event EventHandler? SystemThemeReadFailed;

        public void SetPreference(ThemePreference preference)
        {
            if (Failure is not null)
            {
                throw Failure;
            }

            Preference = preference;
        }

        public void RaiseSystemThemeChanged() => SystemThemeChanged?.Invoke(this, EventArgs.Empty);
        public void RaiseSystemThemeReadFailed() => SystemThemeReadFailed?.Invoke(this, EventArgs.Empty);
    }
}
