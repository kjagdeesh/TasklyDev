using TasklyDev.Core.Tokens;
using TasklyDev.Services;
using TasklyDev.ViewModels;
using Xunit;

namespace TasklyDev.Tests;

public sealed class JwtDecoderViewModelTests
{
    [Fact]
    public void DecodeDisplaysJsonClaimsAndPersistentUntrustedWarning()
    {
        var clipboard = new RecordingClipboard();
        var viewModel = CreateViewModel(clipboard);
        viewModel.TokenText = MakeToken("""{"alg":"HS256","typ":"JWT"}""", """{"sub":"user","exp":1735689600}""");

        viewModel.DecodeCommand.Execute(null);

        Assert.True(viewModel.HasDecodedContent);
        Assert.False(viewModel.IsError);
        Assert.Contains("\"alg\": \"HS256\"", viewModel.HeaderJson);
        Assert.Contains("\"sub\": \"user\"", viewModel.PayloadJson);
        Assert.Contains("NOT verify the signature", viewModel.UntrustedDataWarning);
        Assert.Contains("not verified", viewModel.StatusMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not verified", viewModel.SignatureSegmentStatus, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(viewModel.Claims, claim => claim.Name == "exp" && claim.TimeInterpretation is not null);
    }

    [Fact]
    public void InvalidInputDoesNotExposeRawTokenInStatus()
    {
        const string token = "this.is.not.a.jwt";
        var viewModel = CreateViewModel();
        viewModel.TokenText = token;

        viewModel.DecodeCommand.Execute(null);

        Assert.False(viewModel.HasDecodedContent);
        Assert.True(viewModel.IsError);
        Assert.DoesNotContain(token, viewModel.StatusMessage);
        Assert.Empty(viewModel.HeaderJson);
        Assert.Empty(viewModel.PayloadJson);
    }

    [Fact]
    public void CopyHeaderAndPayloadReportClipboardFailures()
    {
        var clipboard = new RecordingClipboard { Succeeds = false };
        var viewModel = CreateViewModel(clipboard);
        viewModel.TokenText = MakeToken("""{"alg":"HS256"}""", """{"sub":"value"}""");
        viewModel.DecodeCommand.Execute(null);

        viewModel.CopyHeaderCommand.Execute(null);

        Assert.True(viewModel.IsError);
        Assert.Contains("Clipboard is unavailable", viewModel.StatusMessage);
        Assert.Equal(viewModel.HeaderJson, clipboard.Value);

        viewModel.CopyPayloadCommand.Execute(null);

        Assert.Equal(viewModel.PayloadJson, clipboard.Value);
    }

    [Fact]
    public void ClearRemovesTokenAndDecodedDataButKeepsWarning()
    {
        var viewModel = CreateViewModel();
        viewModel.TokenText = MakeToken("""{"alg":"HS256"}""", """{"sub":"value"}""");
        viewModel.DecodeCommand.Execute(null);

        viewModel.ClearCommand.Execute(null);

        Assert.Empty(viewModel.TokenText);
        Assert.False(viewModel.HasDecodedContent);
        Assert.Empty(viewModel.Claims);
        Assert.Contains("NOT verify", viewModel.UntrustedDataWarning);
    }

    private static JwtDecoderViewModel CreateViewModel(RecordingClipboard? clipboard = null) =>
        new(new JwtDecoderService(), clipboard ?? new RecordingClipboard());

    private static string MakeToken(string header, string payload) =>
        $"{Encode(header)}.{Encode(payload)}.c2ln";

    private static string Encode(string value) =>
        Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(value))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

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
