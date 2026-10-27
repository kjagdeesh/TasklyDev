using TasklyDev.Core.Conversion;
using TasklyDev.Services;
using TasklyDev.ViewModels;
using Xunit;

namespace TasklyDev.Tests;

public sealed class NumberBaseViewModelTests
{
    [Fact]
    public void ConvertsAndAppliesQuickPresets()
    {
        var viewModel = CreateViewModel();
        viewModel.InputText = "255";
        viewModel.ConvertCommand.Execute(null);

        Assert.Equal("FF", viewModel.OutputText);
        Assert.False(viewModel.IsError);

        viewModel.ApplyPresetCommand.Execute("DEC_BIN");
        Assert.Equal(10, viewModel.SourceBase);
        Assert.Equal(2, viewModel.TargetBase);
        Assert.Equal("11111111", viewModel.OutputText);
    }

    [Fact]
    public void InvalidInputShowsInlineErrorAndClearsOutput()
    {
        var viewModel = CreateViewModel();
        viewModel.InputText = "42";
        viewModel.ConvertCommand.Execute(null);
        viewModel.SourceBase = 2;
        viewModel.InputText = "102";

        viewModel.ConvertCommand.Execute(null);

        Assert.Empty(viewModel.OutputText);
        Assert.True(viewModel.IsError);
        Assert.Contains("not a valid base-2 digit", viewModel.StatusMessage);
    }

    [Fact]
    public void SwapBasesRecalculatesTheCurrentValue()
    {
        var viewModel = CreateViewModel();
        viewModel.InputText = "255";
        viewModel.ConvertCommand.Execute(null);

        viewModel.SwapBasesCommand.Execute(null);

        Assert.Equal(16, viewModel.SourceBase);
        Assert.Equal(10, viewModel.TargetBase);
        Assert.Equal("FF", viewModel.InputText);
        Assert.Equal("255", viewModel.OutputText);
    }

    [Fact]
    public void ClipboardFailureIsReported()
    {
        var viewModel = CreateViewModel();
        viewModel.InputText = "255";
        viewModel.ConvertCommand.Execute(null);

        viewModel.CopyOutputCommand.Execute(null);

        Assert.True(viewModel.IsError);
        Assert.Contains("Clipboard is unavailable", viewModel.StatusMessage);
    }

    private static NumberBaseViewModel CreateViewModel() =>
        new(new NumberBaseService(), new UnavailableClipboard());

    private sealed class UnavailableClipboard : IClipboardService
    {
        public bool TrySetText(string text) => false;
    }
}
