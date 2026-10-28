using TasklyDev.Core.Colors;
using TasklyDev.Services;
using TasklyDev.ViewModels;
using System.ComponentModel;
using System.Windows.Media;
using Xunit;

namespace TasklyDev.Tests;

public sealed class ColorConverterViewModelTests
{
    [Fact]
    public void ConvertsInputLiveAndUpdatesPreviewColor()
    {
        var viewModel = CreateViewModel();

        viewModel.InputText = "#3A86FFB3";

        Assert.True(viewModel.HasColor);
        Assert.False(viewModel.IsError);
        Assert.Equal("#3A86FFB3", viewModel.HexValue);
        Assert.Equal("rgba(58, 134, 255, 179)", viewModel.RgbValue);
        Assert.Contains(", 0.702)", viewModel.HslValue);
    }

    [Fact]
    public void SelectedFormatControlsLiveInputValidation()
    {
        var viewModel = CreateViewModel();
        viewModel.SelectedFormat = ColorInputFormat.Rgb;
        viewModel.InputText = "rgb(0, 128, 255)";

        Assert.True(viewModel.HasColor);
        Assert.Equal("#0080FF", viewModel.HexValue);

        viewModel.InputText = "rgb(256, 0, 0)";

        Assert.False(viewModel.HasColor);
        Assert.True(viewModel.IsError);
        Assert.Empty(viewModel.HexValue);
        Assert.Equal(0, Assert.IsType<SolidColorBrush>(viewModel.PreviewBrush).Color.A);
    }

    [Fact]
    public void CopiesConvertedValueAndSurfacesClipboardFailure()
    {
        var clipboard = new RecordingClipboard();
        var viewModel = CreateViewModel(clipboard);

        viewModel.CopyHexCommand.Execute(null);

        Assert.Equal("#F5B942", clipboard.Value);
        Assert.False(viewModel.IsError);

        clipboard.Succeeds = false;
        viewModel.CopyRgbCommand.Execute(null);

        Assert.True(viewModel.IsError);
        Assert.Contains("Could not copy", viewModel.StatusMessage);
    }

    [Fact]
    public void ClearAndSampleUpdateInputAndResults()
    {
        var viewModel = CreateViewModel();

        viewModel.ClearCommand.Execute(null);
        Assert.False(viewModel.HasColor);
        Assert.Empty(viewModel.HexValue);

        viewModel.LoadSampleCommand.Execute(null);
        Assert.Equal("#3A86FFB3", viewModel.InputText);
        Assert.True(viewModel.HasColor);
    }

    [Fact]
    public async Task ColorSelectionUpdatesInputAndConvertedValues()
    {
        var picker = new FixedColorPicker(new ColorValue(12, 34, 56), null);
        var viewModel = CreateViewModel(colorPicker: picker);

        await viewModel.PickColorAsync();

        Assert.Equal("#0C2238", viewModel.InputText);
        Assert.Equal("#0C2238", viewModel.HexValue);
        Assert.False(viewModel.IsError);
        Assert.Contains("selected", viewModel.StatusMessage);
        Assert.False(viewModel.IsPickingColor);
        Assert.Equal(new ColorValue(245, 185, 66), picker.InitialColor);
    }

    [Fact]
    public async Task CancellingColorSelectionKeepsCurrentColor()
    {
        var viewModel = CreateViewModel(colorPicker: new FixedColorPicker(null, null));

        await viewModel.PickColorAsync();

        Assert.Equal("#F5B942", viewModel.InputText);
        Assert.Equal("#F5B942", viewModel.HexValue);
        Assert.False(viewModel.IsError);
        Assert.Contains("canceled", viewModel.StatusMessage);
    }

    [Fact]
    public async Task ColorDialogFailureIsShownInline()
    {
        var viewModel = CreateViewModel(colorPicker: new FixedColorPicker(null, new Win32Exception()));

        await viewModel.PickColorAsync();

        Assert.True(viewModel.IsError);
        Assert.Contains("could not open", viewModel.StatusMessage);
    }

    private static ColorConverterViewModel CreateViewModel(
        RecordingClipboard? clipboard = null,
        IColorPickerService? colorPicker = null) =>
        new(new ColorService(), clipboard ?? new RecordingClipboard(), colorPicker);

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

    private sealed class FixedColorPicker(ColorValue? color, Exception? failure) : IColorPickerService
    {
        public ColorValue? InitialColor { get; private set; }

        public Task<ColorValue?> PickColorAsync(ColorValue initialColor)
        {
            InitialColor = initialColor;
            return failure is null ? Task.FromResult(color) : Task.FromException<ColorValue?>(failure);
        }
    }
}
