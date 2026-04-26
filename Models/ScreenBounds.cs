using System.Drawing;

namespace AOUU.Models;

public sealed class ScreenBounds
{
    public int X { get; set; }

    public int Y { get; set; }

    public int Width { get; set; }

    public int Height { get; set; }

    public Rectangle ToRectangle()
    {
        return new Rectangle(X, Y, Width, Height);
    }

    public static ScreenBounds FromRectangle(Rectangle rectangle)
    {
        return new ScreenBounds
        {
            X = rectangle.X,
            Y = rectangle.Y,
            Width = rectangle.Width,
            Height = rectangle.Height
        };
    }

    public override string ToString()
    {
        return $"{X},{Y} {Width}x{Height}";
    }
}
