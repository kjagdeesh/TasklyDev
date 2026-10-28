using TasklyDev.Windows.ClipboardHistory;
using Xunit;

namespace TasklyDev.Tests;

public sealed class DpapiClipboardTextProtectorTests
{
    [Fact]
    public void ProtectAndUnprotectRoundTripForCurrentWindowsUser()
    {
        var protector = new DpapiClipboardTextProtector();
        const string text = "private clipboard content";

        var protectedText = protector.Protect(text);

        Assert.DoesNotContain(text, protectedText);
        Assert.Equal(text, protector.Unprotect(protectedText));
    }

    [Fact]
    public void InvalidPayloadIsReportedAsCryptographicFailure()
    {
        var protector = new DpapiClipboardTextProtector();

        Assert.Throws<System.Security.Cryptography.CryptographicException>(() =>
            protector.Unprotect("not base64"));
    }
}
