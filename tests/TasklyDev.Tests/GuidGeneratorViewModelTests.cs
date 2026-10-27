using TasklyDev.Core.Identifiers;
using TasklyDev.Services;
using TasklyDev.ViewModels;
using Xunit;

namespace TasklyDev.Tests;

public sealed class GuidGeneratorViewModelTests
{
    [Fact]
    public void GeneratesRequestedQuantityAndFormat()
    {
        var viewModel = CreateViewModel();
        viewModel.QuantityInput = "3";
        viewModel.SelectedFormat = GuidOutputFormat.N;

        viewModel.GenerateCommand.Execute(null);

        Assert.Equal(3, viewModel.Values.Count);
        Assert.All(viewModel.Values, value => Assert.Matches("^[0-9a-f]{32}$", value));
        Assert.Equal("Generated 3 GUIDs", viewModel.ResultHeading);
        Assert.False(viewModel.IsError);
    }

    [Theory]
    [InlineData("")]
    [InlineData("two")]
    [InlineData("0")]
    [InlineData("501")]
    public void InvalidQuantityShowsErrorAndDoesNotKeepOldResults(string quantity)
    {
        var viewModel = CreateViewModel();
        viewModel.GenerateCommand.Execute(null);
        viewModel.QuantityInput = quantity;

        viewModel.GenerateCommand.Execute(null);

        Assert.Empty(viewModel.Values);
        Assert.True(viewModel.IsError);
    }

    [Fact]
    public void CopyAllCopiesOneValuePerLine()
    {
        var clipboard = new RecordingClipboard();
        var viewModel = new GuidGeneratorViewModel(new GuidService(), clipboard)
        {
            QuantityInput = "2"
        };
        viewModel.GenerateCommand.Execute(null);

        viewModel.CopyAllCommand.Execute(null);

        Assert.Equal(string.Join(Environment.NewLine, viewModel.Values), clipboard.Value);
    }

    [Fact]
    public void CopyFailureIsReported()
    {
        var viewModel = CreateViewModel();
        viewModel.GenerateCommand.Execute(null);
        viewModel.CopyItemCommand.Execute(viewModel.Values[0]);

        Assert.True(viewModel.IsError);
        Assert.Contains("Clipboard is unavailable", viewModel.StatusMessage);
    }

    [Fact]
    public void ResetRestoresDefaultsAndClearsGeneratedValues()
    {
        var viewModel = CreateViewModel();
        viewModel.QuantityInput = "4";
        viewModel.SelectedFormat = GuidOutputFormat.X;
        viewModel.Uppercase = true;
        viewModel.GenerateCommand.Execute(null);

        viewModel.ResetCommand.Execute(null);

        Assert.Equal("1", viewModel.QuantityInput);
        Assert.Equal(GuidOutputFormat.D, viewModel.SelectedFormat);
        Assert.False(viewModel.Uppercase);
        Assert.Empty(viewModel.Values);
    }

    private static GuidGeneratorViewModel CreateViewModel() =>
        new(new GuidService(), new UnavailableClipboard());

    private sealed class UnavailableClipboard : IClipboardService
    {
        public bool TrySetText(string text) => false;
    }

    private sealed class RecordingClipboard : IClipboardService
    {
        public string? Value { get; private set; }

        public bool TrySetText(string text)
        {
            Value = text;
            return true;
        }
    }
}
