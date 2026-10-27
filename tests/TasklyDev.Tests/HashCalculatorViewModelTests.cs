using TasklyDev.Core.Hashing;
using TasklyDev.Models;
using TasklyDev.Services;
using TasklyDev.ViewModels;
using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace TasklyDev.Tests;

public sealed class HashCalculatorViewModelTests
{
    [Fact]
    public async Task CalculatesTextHashesAndCopiesDigest()
    {
        var clipboard = new RecordingClipboard();
        var viewModel = CreateViewModel(clipboard: clipboard);
        viewModel.InputText = "The quick brown fox jumps over the lazy dog";
        viewModel.SelectedAlgorithm = HashAlgorithmChoice.SHA256;

        await viewModel.CalculateAsync();

        var result = Assert.Single(viewModel.Results);
        Assert.Equal(HashAlgorithmKind.SHA256, result.Algorithm);
        Assert.Equal("d7a8fbb307d7809469ca9abcb0082e4f8d5651e46d3cdb762d02d0bf37c9e592", result.HexValue);
        Assert.False(viewModel.IsBusy);

        viewModel.CopyDigestCommand.Execute(result);
        Assert.Equal(result.HexValue, clipboard.Value);
    }

    [Fact]
    public async Task CalculatesFileHashesThroughSelectedPath()
    {
        var picker = new FixedFilePicker("chosen.txt");
        var viewModel = CreateViewModel(new FixedFileHashService(), filePicker: picker);
        viewModel.BrowseFileCommand.Execute(null);

        Assert.Equal("chosen.txt", viewModel.FilePath);
        Assert.Equal(HashInputMode.File, viewModel.InputMode);

        await viewModel.CalculateAsync();

        Assert.Equal(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("chosen.txt"))).ToLowerInvariant(), viewModel.Results.Single().HexValue);
        Assert.Contains("completed", viewModel.StatusMessage);
    }

    [Fact]
    public async Task FileModeRequiresASelectedPath()
    {
        var viewModel = CreateViewModel();
        viewModel.InputMode = HashInputMode.File;

        await viewModel.CalculateAsync();

        Assert.True(viewModel.IsError);
        Assert.Empty(viewModel.Results);
        Assert.Contains("Choose a file", viewModel.StatusMessage);
    }

    [Fact]
    public async Task FileReadFailureIsShownWithoutLeakingThePath()
    {
        var viewModel = CreateViewModel(new ThrowingHashService());
        viewModel.InputMode = HashInputMode.File;
        viewModel.FilePath = "private-path.txt";

        await viewModel.CalculateAsync();

        Assert.True(viewModel.IsError);
        Assert.DoesNotContain("private-path.txt", viewModel.StatusMessage);
        Assert.Contains("could not be found", viewModel.StatusMessage);
    }

    [Fact]
    public async Task TextModeHashesEmptyTextAndClearRemovesResults()
    {
        var viewModel = CreateViewModel();

        await viewModel.CalculateAsync();

        Assert.Equal(3, viewModel.Results.Count);
        Assert.True(viewModel.HasResults);

        viewModel.ClearCommand.Execute(null);

        Assert.Empty(viewModel.Results);
        Assert.Empty(viewModel.InputText);
        Assert.True(viewModel.IsEmpty);
    }

    private static HashCalculatorViewModel CreateViewModel(
        IHashService? service = null,
        IClipboardService? clipboard = null,
        IFilePickerService? filePicker = null) =>
        new(service ?? new HashService(), clipboard ?? new RecordingClipboard(), filePicker ?? new FixedFilePicker(null));

    private sealed class RecordingClipboard : IClipboardService
    {
        public string? Value { get; private set; }

        public bool TrySetText(string text)
        {
            Value = text;
            return true;
        }
    }

    private sealed class FixedFilePicker(string? path) : IFilePickerService
    {
        public string? PickFile() => path;
    }

    private sealed class ThrowingHashService : IHashService
    {
        public IReadOnlyList<HashDigest> ComputeText(string text, IReadOnlyCollection<HashAlgorithmKind> algorithms) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<HashDigest>> ComputeFileAsync(
            string filePath,
            IReadOnlyCollection<HashAlgorithmKind> algorithms,
            CancellationToken cancellationToken = default) =>
            Task.FromException<IReadOnlyList<HashDigest>>(new FileNotFoundException());

        public Task<IReadOnlyList<HashDigest>> ComputeStreamAsync(
            Stream input,
            IReadOnlyCollection<HashAlgorithmKind> algorithms,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class FixedFileHashService : IHashService
    {
        private readonly HashService _inner = new();

        public IReadOnlyList<HashDigest> ComputeText(string text, IReadOnlyCollection<HashAlgorithmKind> algorithms) =>
            _inner.ComputeText(text, algorithms);

        public Task<IReadOnlyList<HashDigest>> ComputeFileAsync(
            string filePath,
            IReadOnlyCollection<HashAlgorithmKind> algorithms,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<HashDigest>>(
                [new HashDigest(algorithms.First(), Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(filePath))).ToLowerInvariant())]);

        public Task<IReadOnlyList<HashDigest>> ComputeStreamAsync(
            Stream input,
            IReadOnlyCollection<HashAlgorithmKind> algorithms,
            CancellationToken cancellationToken = default) =>
            _inner.ComputeStreamAsync(input, algorithms, cancellationToken);
    }
}
