using TasklyDev.Core.Encoding;
using TasklyDev.Services;
using TasklyDev.ViewModels;
using Xunit;

namespace TasklyDev.Tests;

public sealed class UrlCodecViewModelTests
{
    [Fact]
    public void RunCommandEncodesAndReportsModeDescription()
    {
        var viewModel = CreateViewModel();
        viewModel.InputText = "two words+a";
        viewModel.SelectedMode = UrlEncodingMode.QueryValue;

        viewModel.RunCommand.Execute(null);

        Assert.Equal("two+words%2Ba", viewModel.OutputText);
        Assert.False(viewModel.IsError);
        Assert.Contains("Spaces become +", viewModel.ModeDescription);
    }

    [Fact]
    public void DecodeCommandAppliesQueryFormPlusRules()
    {
        var viewModel = CreateViewModel();
        viewModel.InputText = "hello+world%2B";
        viewModel.SelectedMode = UrlEncodingMode.QueryValue;

        viewModel.DecodeCommand.Execute(null);

        Assert.Equal("hello world+", viewModel.OutputText);
        Assert.Contains("decoded successfully", viewModel.StatusMessage);
    }

    [Fact]
    public void FullUrlModeDisplaysParsedComponents()
    {
        var viewModel = CreateViewModel();
        viewModel.InputText = "https://example.com/a b?q=x y#part";
        viewModel.SelectedMode = UrlEncodingMode.FullUrl;

        viewModel.EncodeCommand.Execute(null);

        Assert.Equal("https://example.com/a%20b?q=x%20y#part", viewModel.OutputText);
        Assert.True(viewModel.HasComponents);
        Assert.Equal("https", viewModel.Components?.Scheme);
        Assert.Equal("example.com", viewModel.Components?.Authority);
    }

    [Fact]
    public void InvalidInputClearsOldOutputAndShowsInlineError()
    {
        var viewModel = CreateViewModel();
        viewModel.InputText = "safe value";
        viewModel.RunCommand.Execute(null);
        viewModel.IsEncoding = false;
        viewModel.InputText = "%GG";

        viewModel.RunCommand.Execute(null);

        Assert.Empty(viewModel.OutputText);
        Assert.True(viewModel.IsError);
        Assert.Contains("malformed", viewModel.StatusMessage);
    }

    [Fact]
    public void SwapMovesOutputAndTogglesOperation()
    {
        var viewModel = CreateViewModel();
        viewModel.InputText = "space here";
        viewModel.RunCommand.Execute(null);
        var encoded = viewModel.OutputText;

        viewModel.SwapCommand.Execute(null);

        Assert.Equal(encoded, viewModel.InputText);
        Assert.Empty(viewModel.OutputText);
        Assert.False(viewModel.IsEncoding);
    }

    [Fact]
    public void CopyFailureIsReported()
    {
        var clipboard = new RecordingClipboard { Succeeds = false };
        var viewModel = CreateViewModel(clipboard);
        viewModel.InputText = "copy me";
        viewModel.RunCommand.Execute(null);

        viewModel.CopyOutputCommand.Execute(null);

        Assert.True(viewModel.IsError);
        Assert.Contains("Clipboard is unavailable", viewModel.StatusMessage);
        Assert.Equal("copy%20me", clipboard.Value);
    }

    [Fact]
    public void ClearResetsInputOutputAndParsedComponents()
    {
        var viewModel = CreateViewModel();
        viewModel.InputText = "https://example.com/path";
        viewModel.SelectedMode = UrlEncodingMode.FullUrl;
        viewModel.RunCommand.Execute(null);

        viewModel.ClearCommand.Execute(null);

        Assert.Empty(viewModel.InputText);
        Assert.Empty(viewModel.OutputText);
        Assert.False(viewModel.HasComponents);
    }

    private static UrlCodecViewModel CreateViewModel(RecordingClipboard? clipboard = null) =>
        new(new UrlCodecService(), clipboard ?? new RecordingClipboard());

    private sealed class RecordingClipboard : IClipboardService
    {
        public string? Value { get; private set; }
        public bool Succeeds { get; set; } = true;

        public bool TrySetText(string text)
        {
            Value = text;
            return Succeeds;
        }
    }
}
