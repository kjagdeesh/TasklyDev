using TasklyDev.Core.Time;
using TasklyDev.Services;
using TasklyDev.ViewModels;
using Xunit;

namespace TasklyDev.Tests;

public sealed class UnixTimestampViewModelTests
{
    [Fact]
    public void TimestampModeShowsBothUtcAndLocalTimeWithUnitSpecificValues()
    {
        var viewModel = CreateViewModel();
        viewModel.TimestampInput = "0";
        viewModel.SelectedUnit = UnixTimestampUnit.Seconds;
        viewModel.ConvertTimestampCommand.Execute(null);

        Assert.StartsWith("1970-01-01T00:00:00", viewModel.UtcResult);
        Assert.Contains("Z", viewModel.UtcResult);
        Assert.NotEmpty(viewModel.LocalResult);
        Assert.Equal("0", viewModel.SecondsResult);
        Assert.Equal("0", viewModel.MillisecondsResult);
        Assert.False(viewModel.IsError);
    }

    [Fact]
    public void MillisecondsUnitIsAppliedOnlyWhenExplicitlySelected()
    {
        var viewModel = CreateViewModel();
        viewModel.TimestampInput = "1000";
        viewModel.SelectedUnit = UnixTimestampUnit.Milliseconds;

        viewModel.ConvertTimestampCommand.Execute(null);

        Assert.StartsWith("1970-01-01T00:00:01", viewModel.UtcResult);
        Assert.Equal("1", viewModel.SecondsResult);
        Assert.Equal("1000", viewModel.MillisecondsResult);
    }

    [Fact]
    public void DateModeRejectsInputWithoutExplicitTimezone()
    {
        var viewModel = CreateViewModel();
        viewModel.DateTimeInput = "2024-06-11T11:34:40";

        viewModel.ConvertDateCommand.Execute(null);

        Assert.True(viewModel.IsError);
        Assert.Empty(viewModel.SecondsResult);
        Assert.Contains("explicit UTC marker", viewModel.StatusMessage);
    }

    [Fact]
    public void DateModeProducesSecondsAndMillisecondsFromExplicitOffset()
    {
        var viewModel = CreateViewModel();
        viewModel.DateTimeInput = "2024-06-11T17:04:40+05:30";

        viewModel.ConvertDateCommand.Execute(null);

        Assert.Equal("1718105680", viewModel.SecondsResult);
        Assert.Equal("1718105680000", viewModel.MillisecondsResult);
        Assert.Equal("2024-06-11T11:34:40.000Z", viewModel.UtcResult);
        Assert.False(viewModel.IsError);
    }

    [Fact]
    public void InvalidTimestampClearsOldResultsAndShowsError()
    {
        var viewModel = CreateViewModel();
        viewModel.TimestampInput = "0";
        viewModel.ConvertTimestampCommand.Execute(null);
        viewModel.TimestampInput = "not-a-number";

        viewModel.ConvertTimestampCommand.Execute(null);

        Assert.Empty(viewModel.UtcResult);
        Assert.True(viewModel.IsError);
        Assert.Contains("whole-number", viewModel.StatusMessage);
    }

    [Fact]
    public void ClipboardFailureIsReported()
    {
        var viewModel = CreateViewModel();
        viewModel.TimestampInput = "0";
        viewModel.ConvertTimestampCommand.Execute(null);

        viewModel.CopyUtcCommand.Execute(null);

        Assert.True(viewModel.IsError);
        Assert.Contains("Clipboard is unavailable", viewModel.StatusMessage);
    }

    private static UnixTimestampViewModel CreateViewModel() =>
        new(new TimestampService(), new UnavailableClipboard());

    private sealed class UnavailableClipboard : IClipboardService
    {
        public bool TrySetText(string text) => false;
    }
}
