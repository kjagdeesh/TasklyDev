using TasklyDev.Core.RegularExpressions;
using TasklyDev.ViewModels;
using Xunit;

namespace TasklyDev.Tests;

public sealed class RegexTesterViewModelTests
{
    [Fact]
    public async Task RunsSampleAndReportsMatchCount()
    {
        var viewModel = new RegexTesterViewModel(new RegexTesterService());

        await viewModel.TestAsync();

        Assert.Equal(2, viewModel.Matches.Count);
        Assert.False(viewModel.IsBusy);
        Assert.False(viewModel.IsError);
        Assert.Contains("Found 2 matches", viewModel.StatusMessage);
    }

    [Fact]
    public async Task InvalidPatternShowsInlineErrorAndClearsPreviousResults()
    {
        var viewModel = new RegexTesterViewModel(new RegexTesterService());
        await viewModel.TestAsync();

        viewModel.Pattern = "(";
        await viewModel.TestAsync();

        Assert.True(viewModel.IsError);
        Assert.Empty(viewModel.Matches);
        Assert.Contains("Invalid regular expression", viewModel.StatusMessage);
    }

    [Fact]
    public async Task TimeoutIsHandledWithActionableInlineFeedback()
    {
        var viewModel = new RegexTesterViewModel(new RegexTesterService(TimeSpan.FromMilliseconds(10)))
        {
            Pattern = "^(a+)+$",
            InputText = string.Concat(new string('a', 50_000), "!")
        };

        await viewModel.TestAsync();

        Assert.True(viewModel.IsError);
        Assert.Contains("timed out", viewModel.StatusMessage);
        Assert.False(viewModel.IsBusy);
    }

    [Fact]
    public async Task EmptyPatternRequiresInputAndClearResetsTheScreen()
    {
        var viewModel = new RegexTesterViewModel(new RegexTesterService())
        {
            Pattern = string.Empty
        };

        await viewModel.TestAsync();

        Assert.True(viewModel.IsError);
        Assert.Contains("Enter a regular expression", viewModel.StatusMessage);

        viewModel.ClearCommand.Execute(null);

        Assert.Empty(viewModel.Pattern);
        Assert.Empty(viewModel.InputText);
        Assert.False(viewModel.HasTested);
    }

    [Fact]
    public async Task NoMatchHasExplicitEmptyState()
    {
        var viewModel = new RegexTesterViewModel(new RegexTesterService())
        {
            Pattern = "missing",
            InputText = "some text"
        };

        await viewModel.TestAsync();

        Assert.True(viewModel.HasNoMatches);
        Assert.Equal("No matches found.", viewModel.StatusMessage);
    }
}
