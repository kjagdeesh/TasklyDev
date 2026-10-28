using Xunit;
using TasklyDev.ViewModels;

namespace TasklyDev.Tests;

public sealed class ShellViewModelTests
{
    [Fact]
    public void StartsOnOverviewWithAllToolsVisible()
    {
        var viewModel = new MainWindowViewModel();

        Assert.True(viewModel.IsHome);
        Assert.Equal(10, viewModel.DeveloperTools.Count);
        Assert.Equal(2, viewModel.SystemTools.Count);
        Assert.Equal("jwt-decoder", viewModel.DeveloperNavigation.Last().Route);
        Assert.True(viewModel.OverviewNavigation.IsActive);
    }

    [Fact]
    public void SearchFiltersToolsAndShowsEmptyStateWhenNoToolsMatch()
    {
        var viewModel = new MainWindowViewModel();

        viewModel.SearchText = "timestamp";
        Assert.Single(viewModel.DeveloperTools);
        Assert.Empty(viewModel.SystemTools);
        Assert.False(viewModel.HasNoSearchResults);

        viewModel.SearchText = "not-a-tool";
        Assert.Empty(viewModel.DeveloperTools);
        Assert.Empty(viewModel.SystemTools);
        Assert.True(viewModel.HasNoSearchResults);
    }

    [Fact]
    public void NavigationSelectsToolPlaceholderAndReturnsToOverview()
    {
        var viewModel = new MainWindowViewModel();

        viewModel.OpenRouteCommand.Execute("base64");
        Assert.False(viewModel.IsHome);
        Assert.Equal("Base64 Encoder & Decoder", viewModel.CurrentPageTitle);
        Assert.True(viewModel.DeveloperNavigation.Single(item => item.Route == "base64").IsActive);

        viewModel.OpenRouteCommand.Execute("home");
        Assert.True(viewModel.IsHome);
        Assert.True(viewModel.OverviewNavigation.IsActive);
    }

    [Fact]
    public void TimestampRouteOpensTheTimestampScreen()
    {
        var viewModel = new MainWindowViewModel();

        viewModel.OpenRouteCommand.Execute("timestamp");

        Assert.True(viewModel.IsTimestamp);
        Assert.False(viewModel.IsPlaceholder);
        Assert.Equal("Unix Timestamp Converter", viewModel.CurrentPageTitle);
    }

    [Fact]
    public void GuidRouteOpensTheGuidGeneratorScreen()
    {
        var viewModel = new MainWindowViewModel();

        viewModel.OpenRouteCommand.Execute("guid");

        Assert.True(viewModel.IsGuidGenerator);
        Assert.False(viewModel.IsPlaceholder);
        Assert.Equal("GUID Generator", viewModel.CurrentPageTitle);
    }

    [Fact]
    public void GuidParserRouteOpensTheUuidGuidParserScreen()
    {
        var viewModel = new MainWindowViewModel();

        viewModel.OpenRouteCommand.Execute("uuid-parser");

        Assert.True(viewModel.IsGuidParser);
        Assert.False(viewModel.IsPlaceholder);
        Assert.Equal("UUID / GUID Validator & Parser", viewModel.CurrentPageTitle);
        Assert.True(viewModel.DeveloperNavigation.Single(item => item.Route == "uuid-parser").IsActive);
    }

    [Fact]
    public void UrlCodecRouteOpensUrlEncoderDecoderAndFollowsExistingDeveloperTools()
    {
        var viewModel = new MainWindowViewModel();

        viewModel.OpenRouteCommand.Execute("url-codec");

        Assert.True(viewModel.IsUrlCodec);
        Assert.False(viewModel.IsPlaceholder);
        Assert.Equal("URL Encoder / Decoder", viewModel.CurrentPageTitle);
        Assert.True(viewModel.DeveloperNavigation.Single(item => item.Route == "url-codec").IsActive);
    }

    [Fact]
    public void JwtDecoderRouteOpensTheLocalInspectorAndIsLastDeveloperTool()
    {
        var viewModel = new MainWindowViewModel();

        viewModel.OpenRouteCommand.Execute("jwt-decoder");

        Assert.True(viewModel.IsJwtDecoder);
        Assert.False(viewModel.IsPlaceholder);
        Assert.Equal("JWT Decoder & Inspector", viewModel.CurrentPageTitle);
        Assert.Equal("jwt-decoder", viewModel.DeveloperNavigation.Last().Route);
    }

    [Fact]
    public void HashRouteOpensTheHashCalculatorScreen()
    {
        var viewModel = new MainWindowViewModel();

        viewModel.OpenRouteCommand.Execute("hash");

        Assert.True(viewModel.IsHashCalculator);
        Assert.False(viewModel.IsPlaceholder);
        Assert.Equal("Hash Calculator", viewModel.CurrentPageTitle);
    }

    [Fact]
    public void ColorRouteOpensTheColorConverterScreen()
    {
        var viewModel = new MainWindowViewModel();

        viewModel.OpenRouteCommand.Execute("color");

        Assert.True(viewModel.IsColorConverter);
        Assert.False(viewModel.IsPlaceholder);
        Assert.Equal("Color Converter", viewModel.CurrentPageTitle);
    }

    [Fact]
    public void RegexRouteOpensTheRegexTesterScreen()
    {
        var viewModel = new MainWindowViewModel();

        viewModel.OpenRouteCommand.Execute("regex");

        Assert.True(viewModel.IsRegexTester);
        Assert.False(viewModel.IsPlaceholder);
        Assert.Equal("Regex Tester", viewModel.CurrentPageTitle);
    }

    [Fact]
    public void ProcessRouteOpensTheProcessLookupScreen()
    {
        var viewModel = new MainWindowViewModel();

        viewModel.OpenRouteCommand.Execute("process");

        Assert.True(viewModel.IsProcessLookup);
        Assert.False(viewModel.IsPlaceholder);
        Assert.Equal("Process Lookup", viewModel.CurrentPageTitle);
    }

    [Fact]
    public void EnvironmentRouteOpensTheEnvironmentVariableScreen()
    {
        var viewModel = new MainWindowViewModel();

        viewModel.OpenRouteCommand.Execute("environment");

        Assert.True(viewModel.IsEnvironmentVariables);
        Assert.False(viewModel.IsPlaceholder);
        Assert.Equal("Environment Variables", viewModel.CurrentPageTitle);
    }

    [Fact]
    public void SettingsRouteOpensSettingsAndAboutScreen()
    {
        var viewModel = new MainWindowViewModel();

        viewModel.OpenRouteCommand.Execute("settings");

        Assert.True(viewModel.IsSettings);
        Assert.False(viewModel.IsPlaceholder);
        Assert.Equal("Preferences", viewModel.CurrentPageTitle);
    }
}
