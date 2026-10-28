using TasklyDev.Core.Identifiers;
using TasklyDev.Services;
using TasklyDev.ViewModels;
using Xunit;

namespace TasklyDev.Tests;

public sealed class GuidParserViewModelTests
{
    [Fact]
    public void ParseCommandDisplaysCanonicalValueFormatAndVersion()
    {
        var viewModel = CreateViewModel();
        viewModel.InputText = "{00112233-4455-4677-8899-AABBCCDDEEFF}";

        viewModel.ParseCommand.Execute(null);

        Assert.True(viewModel.HasResult);
        Assert.True(viewModel.IsValid);
        Assert.False(viewModel.IsError);
        Assert.Equal("00112233-4455-4677-8899-aabbccddeeff", viewModel.CanonicalValue);
        Assert.Equal("B", viewModel.DetectedFormat);
        Assert.Equal("Version 4", viewModel.VersionDescription);
    }

    [Fact]
    public void InvalidInputShowsErrorAndDoesNotProduceCanonicalValue()
    {
        var viewModel = CreateViewModel();
        viewModel.InputText = "invalid";

        viewModel.ParseCommand.Execute(null);

        Assert.True(viewModel.HasResult);
        Assert.False(viewModel.IsValid);
        Assert.True(viewModel.IsError);
        Assert.Empty(viewModel.CanonicalValue);
        Assert.Contains("Use a valid", viewModel.StatusMessage);
    }

    [Fact]
    public void CopyRequiresValidInputAndSurfacesClipboardFailure()
    {
        var clipboard = new RecordingClipboard { Succeeds = false };
        var viewModel = CreateViewModel(clipboard);
        viewModel.InputText = "00112233-4455-4677-8899-aabbccddeeff";
        viewModel.ParseCommand.Execute(null);

        viewModel.CopyCanonicalCommand.Execute(null);

        Assert.True(viewModel.IsError);
        Assert.Contains("Clipboard is unavailable", viewModel.StatusMessage);
        Assert.Equal("00112233-4455-4677-8899-aabbccddeeff", clipboard.Value);
    }

    [Fact]
    public void ClearResetsInputAndParseResult()
    {
        var viewModel = CreateViewModel();
        viewModel.InputText = "00112233-4455-4677-8899-aabbccddeeff";
        viewModel.ParseCommand.Execute(null);

        viewModel.ClearCommand.Execute(null);

        Assert.Empty(viewModel.InputText);
        Assert.False(viewModel.HasResult);
        Assert.False(viewModel.IsError);
        Assert.Empty(viewModel.CanonicalValue);
    }

    private static GuidParserViewModel CreateViewModel(RecordingClipboard? clipboard = null) =>
        new(new GuidParserService(), clipboard ?? new RecordingClipboard());

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
