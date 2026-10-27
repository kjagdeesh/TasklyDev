using TasklyDev.Core.Encoding;
using TasklyDev.Services;
using TasklyDev.ViewModels;
using Xunit;

namespace TasklyDev.Tests;

public sealed class Base64ViewModelTests
{
    [Fact]
    public void EncodeAndDecodeCommandsUpdateOutputAndStatus()
    {
        var viewModel = CreateViewModel();
        viewModel.InputText = "Hello 🌱";

        viewModel.RunCommand.Execute(null);

        Assert.Equal(Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("Hello 🌱")), viewModel.OutputText);
        Assert.False(viewModel.IsError);

        viewModel.InputText = viewModel.OutputText;
        viewModel.IsEncoding = false;
        viewModel.RunCommand.Execute(null);

        Assert.Equal("Hello 🌱", viewModel.OutputText);
        Assert.Contains("decoded successfully", viewModel.StatusMessage);
    }

    [Fact]
    public void InvalidBase64ShowsInlineErrorAndClearsPreviousOutput()
    {
        var viewModel = CreateViewModel();
        viewModel.InputText = "valid";
        viewModel.RunCommand.Execute(null);
        viewModel.IsEncoding = false;
        viewModel.InputText = "not valid base64!";

        viewModel.RunCommand.Execute(null);

        Assert.Empty(viewModel.OutputText);
        Assert.True(viewModel.IsError);
        Assert.Contains("not valid Base64", viewModel.StatusMessage);
    }

    [Fact]
    public void SwapMovesOutputToInputAndChangesDirection()
    {
        var viewModel = CreateViewModel();
        viewModel.InputText = "TasklyDev";
        viewModel.RunCommand.Execute(null);
        var encoded = viewModel.OutputText;

        viewModel.SwapCommand.Execute(null);

        Assert.Equal(encoded, viewModel.InputText);
        Assert.Empty(viewModel.OutputText);
        Assert.False(viewModel.IsEncoding);
    }

    [Fact]
    public void CopyFailureIsReportedWithoutThrowing()
    {
        var viewModel = new Base64ViewModel(new Base64Service(), new UnavailableClipboard());
        viewModel.InputText = "copy me";
        viewModel.RunCommand.Execute(null);

        viewModel.CopyOutputCommand.Execute(null);

        Assert.True(viewModel.IsError);
        Assert.Contains("Clipboard is unavailable", viewModel.StatusMessage);
    }

    private static Base64ViewModel CreateViewModel() =>
        new(new Base64Service(), new UnavailableClipboard());

    private sealed class UnavailableClipboard : IClipboardService
    {
        public bool TrySetText(string text) => false;
    }
}
