using Avalonia;
using Xunit;

namespace Greenshot.Mac.Tests;

public sealed class SelectionCoordinateMapperTests
{
    [Theory]
    [InlineData(100, 50, 900, 450)]
    [InlineData(900, 50, 100, 450)]
    [InlineData(100, 450, 900, 50)]
    [InlineData(900, 450, 100, 50)]
    public void ToImagePixels_MapsEveryDragDirectionToTheSameRectangle(
        double startX,
        double startY,
        double endX,
        double endY)
    {
        var result = SelectionCoordinateMapper.ToImagePixels(
            new Point(startX, startY),
            new Point(endX, endY),
            new Size(1000, 500),
            2000,
            1000);

        Assert.Equal(new PixelRect(200, 100, 1600, 800), result);
    }

    [Fact]
    public void ToImagePixels_ClampsASelectionBeyondEveryScreenEdge()
    {
        var result = SelectionCoordinateMapper.ToImagePixels(
            new Point(-50, -25),
            new Point(1050, 525),
            new Size(1000, 500),
            2000,
            1000);

        Assert.Equal(new PixelRect(0, 0, 2000, 1000), result);
    }

    [Fact]
    public void ToImagePixels_ReturnsAnEmptyRectangleForZeroAreaSelection()
    {
        var result = SelectionCoordinateMapper.ToImagePixels(
            new Point(200.25, 100.75),
            new Point(200.25, 100.75),
            new Size(1000, 500),
            2000,
            1000);

        Assert.Equal(default, result);
    }

    [Fact]
    public void ToImagePixels_ReturnsEmptyForInvalidOverlayOrImageDimensions()
    {
        var result = SelectionCoordinateMapper.ToImagePixels(
            new Point(0, 0),
            new Point(10, 10),
            new Size(0, 500),
            2000,
            1000);

        Assert.Equal(default, result);
    }

    [Theory]
    [InlineData(1280, 720, 2560, 1440, 100, 50, 1180, 670, 200, 100, 2160, 1240)]
    [InlineData(1536, 864, 1920, 1080, 80, 40, 1456, 824, 100, 50, 1720, 980)]
    public void ToImagePixels_MapsLogicalOverlayCoordinatesAtDifferentDisplayScales(
        double overlayWidth,
        double overlayHeight,
        int imageWidth,
        int imageHeight,
        double startX,
        double startY,
        double endX,
        double endY,
        int expectedX,
        int expectedY,
        int expectedWidth,
        int expectedHeight)
    {
        var result = SelectionCoordinateMapper.ToImagePixels(
            new Point(startX, startY),
            new Point(endX, endY),
            new Size(overlayWidth, overlayHeight),
            imageWidth,
            imageHeight);

        Assert.Equal(new PixelRect(expectedX, expectedY, expectedWidth, expectedHeight), result);
    }
}