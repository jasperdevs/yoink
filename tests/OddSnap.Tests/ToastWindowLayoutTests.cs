using Xunit;
using OddSnap.UI;
using OddSnap.Models;
using System.Windows;

namespace OddSnap.Tests;

public sealed class ToastWindowLayoutTests
{
    [Fact]
    public void ComputeImageOnlyPreviewLayout_UsesConsistentHeightForStandardImages()
    {
        var landscape = ToastWindow.ComputeImageOnlyPreviewLayout(1920, 1080);
        var square = ToastWindow.ComputeImageOnlyPreviewLayout(1080, 1080);

        Assert.False(landscape.Framed);
        Assert.False(square.Framed);
        Assert.InRange(landscape.Height, 180, 188);
        Assert.Equal(188, square.Height);
        Assert.InRange(System.Math.Abs(square.Height - landscape.Height), 0, 8);
    }

    [Fact]
    public void ComputeImageOnlyPreviewLayout_FramesPortraitImages()
    {
        var portrait = ToastWindow.ComputeImageOnlyPreviewLayout(800, 1400);

        Assert.True(portrait.Framed);
        Assert.Equal(188, portrait.Width);
        Assert.Equal(220, portrait.Height);
    }

    [Fact]
    public void GetPlacement_Right_UsesWorkAreaBottomRight()
    {
        var workArea = new Rect(0, 0, 1920, 1040);

        var placement = PopupWindowHelper.GetPlacement(
            ToastPosition.Right,
            actualWidth: 320,
            actualHeight: 120,
            workArea,
            edge: 8);

        Assert.Equal(1592, placement.targetLeft);
        Assert.Equal(912, placement.targetTop);
        Assert.True(placement.animateLeft);
        Assert.True(placement.startLeft > workArea.Right);
    }

    [Fact]
    public void PhysicalPixelsToDips_ConvertsWorkAreaForScaledDisplays()
    {
        var physicalWorkArea = new System.Drawing.Rectangle(0, 0, 2560, 1360);
        var converted = PopupWindowHelper.PhysicalPixelsToDips(
            physicalWorkArea,
            new System.Drawing.Point(100, 100));

        Assert.True(converted.Width <= physicalWorkArea.Width);
        Assert.True(converted.Height <= physicalWorkArea.Height);
        Assert.True(converted.Width > 0);
        Assert.True(converted.Height > 0);
    }
}
