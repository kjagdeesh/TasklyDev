using TasklyDev.Windows.ScreenCapture;
using Xunit;

namespace TasklyDev.Tests;

public sealed class DesktopPixelServiceTests
{
    [Fact]
    public void VirtualDesktopBoundsHavePositiveDimensions()
    {
        var bounds = new DesktopPixelService().GetVirtualDesktopBounds();

        Assert.True(bounds.Width > 0);
        Assert.True(bounds.Height > 0);
    }

    [Fact]
    public void RejectsSamplePointOutsideVirtualDesktopBeforeOpeningScreenDc()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new DesktopPixelService().SamplePixel(int.MaxValue, int.MaxValue));
    }
}
