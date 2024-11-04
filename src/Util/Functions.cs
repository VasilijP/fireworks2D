namespace Fireworks2D.Util;

public static class Func
{
    // RGB format TODO: make sure this is multiplatform compatible
    public static uint EncodePixelColor(int r, int g, int b)
    {
        if (BitConverter.IsLittleEndian) { return (uint)(((b & 0xFF) << 16) | ((g & 0xFF) << 8) | (r & 0xFF)); }
                                           return (uint)(((r & 0xFF) << 16) | ((g & 0xFF) << 8) | (b & 0xFF));
    }

    // ARGB format TODO: make sure this is multiplatform compatible
    public static uint EncodePixelColorRgba(int r, int g, int b, int a)
    {
        if (BitConverter.IsLittleEndian) { return (uint)(((a & 0xFF) << 24) | ((b & 0xFF) << 16) | ((g & 0xFF) << 8) | (r & 0xFF)); }
                                           return (uint)(((r & 0xFF) << 24) | ((g & 0xFF) << 16) | ((b & 0xFF) << 8) | (a & 0xFF));
    }

    public static void DecodePixelColor(uint color, out int r, out int g, out int b)
    {
        if (BitConverter.IsLittleEndian) { r = (int)(color & 0xFF);         g = (int)((color >> 8) & 0xFF); b = (int)((color >> 16) & 0xFF); }
        else                             { r = (int)((color >> 16) & 0xFF); g = (int)((color >> 8) & 0xFF); b = (int)(color & 0xFF); }
    }

    // makes color darker (or lighter) by a given factor
    public static uint Darker(uint barColor, double ratio = 0.75) 
    {
        DecodePixelColor(barColor, out int r, out int g, out int b);
        r = Math.Clamp((int)(r*ratio), 0, 255); g = Math.Clamp((int)(g*ratio), 0, 255); b = Math.Clamp((int)(b*ratio), 0, 255);
        return EncodePixelColor(r, g, b);
    }

    public static uint MixColors(uint color1, float weight1, uint color2, float weight2)
    {
        DecodePixelColor(color1, out int r1, out int g1, out int b1);
        DecodePixelColor(color2, out int r2, out int g2, out int b2);
        r1 = 255 - r1; g1 = 255 - g1; b1 = 255 - b1;
        r2 = 255 - r2; g2 = 255 - g2; b2 = 255 - b2;
        float sumW = weight1 + weight2;
        weight1 /= sumW;  weight2 /= sumW; // normalize weights to have sum = 1.0
        int r = Math.Clamp((int)(r1*weight1 + r2*weight2), 0, 255);
        int g = Math.Clamp((int)(g1*weight1 + g2*weight2), 0, 255);
        int b = Math.Clamp((int)(b1*weight1 + b2*weight2), 0, 255);
        return EncodePixelColor(255-r, 255-g, 255-b);
    }
}
