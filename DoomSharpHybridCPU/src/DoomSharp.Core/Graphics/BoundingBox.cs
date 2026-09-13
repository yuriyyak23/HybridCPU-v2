namespace DoomSharp.Core.Graphics;

public static class BoundingBox
{
    public const int BoxTop = 0;
    public const int BoxBottom = 1;
    public const int BoxLeft = 2;
    public const int BoxRight = 3;

    public static void ClearBox(Fixed[] box)
    {
        box[BoxTop] = box[BoxRight] = Fixed.MinValue;
        box[BoxBottom] = box[BoxLeft] = Fixed.MaxValue;
    }

    public static void AddToBox(Fixed[] box, Fixed x, Fixed y)
    {
        if (x < box[BoxLeft])
        {
            box[BoxLeft] = x;
        }
        else if (x > box[BoxRight])
        {
            box[BoxRight] = x;
        }

        if (y < box[BoxBottom])
        {
            box[BoxBottom] = y;
        }
        else if (y > box[BoxTop])
        {
            box[BoxTop] = y;
        }
    }

    public static void ClearRawBox(int[] box)
    {
        box[BoxTop] = box[BoxRight] = int.MinValue;
        box[BoxBottom] = box[BoxLeft] = int.MaxValue;
    }

    public static void AddRawToBox(int[] box, int x, int y)
    {
        if (x < box[BoxLeft]) box[BoxLeft] = x;
        else if (x > box[BoxRight]) box[BoxRight] = x;

        if (y < box[BoxBottom]) box[BoxBottom] = y;
        else if (y > box[BoxTop]) box[BoxTop] = y;
    }
}
