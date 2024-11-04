using Fireworks2D.Model;
using Fireworks2D.Presentation;

namespace Fireworks2D.Util;

// Canvas for drawing into buffer
public sealed class Canvas(FrameDescriptor frame)
{
    // 8 bit per RGB channel
    private uint penColor;
    private int penX = 0;
    private int penY = 0;
    
    private int clipX = 0;
    private int clipXw = frame.Buffer.Width;
    private int clipY = 0;
    private int clipYh = frame.Buffer.Height;
    
    // font texture
    public static readonly Font Font16X16 = new("resources/texture/oldschool_16x16.tga", 16, 16);
    public static readonly Font Font9X16 = new("resources/texture/oldschool_9x16.tga", 9, 16);
    
    public Canvas SetPenColor(int r, int g, int b) { penColor = Func.EncodePixelColor(r, g, b); return this; }
    public Canvas SetPenColor(uint color) { penColor = color; return this; }

    public Canvas MoveTo(int x, int y) { penX = x; penY = y; return this;}

    // Draws a line from current position to (x,y) but pen position is optionally not changed
    public Canvas DrawTo(int x, int y, bool movePen = false) { int a = penX, b = penY; LineTo(x, y); if (!movePen) { MoveTo(a, b); } return this; }
    
    // Draws a line from current position relative to +(dx, dy) but pen position is not changed
    public Canvas Draw(int dx, int dy, bool movePen = false) { return DrawTo(penX + dx, penY + dy, movePen); }
    
    // Draws a line from current position to (x,y)
    public Canvas LineTo(int x, int y)
    {
        int dx = Math.Abs(x - penX);
        int dy = Math.Abs(y - penY);
        int sx = penX < x ? 1 : -1;
        int sy = penY < y ? 1 : -1;
        int err = dx - dy;

        SetPixel(penX, penY);
        while (penX != x || penY != y)
        {
            SetPixel(penX, penY);
            int e2 = 2 * err;
            if (e2 > -dy) { err -= dy; penX += sx; }
            if (e2 < dx) { err += dx; penY += sy; }
        }
        return this;
    }
    
    public Canvas Line(int dx, int dy) { return LineTo(penX + dx, penY + dy); }

    // clipped and slow, use only for small objects
    public void SetPixel(int x, int y)
    {
        if (x < clipX || x >= clipXw || y < clipY || y >= clipYh) return;
        int index = frame.Offset + x * frame.Buffer.Height + y;
        frame.Buffer.Data[index] = penColor;
    }
    
    public void Rectangle(float positionX, float positionY, int boxWidth, int boxHeight)
    {
        int xStart = (int)Math.Max(clipX, positionX);
        int yStart = (int)Math.Max(clipY, positionY);
        int xEnd = (int)Math.Min(clipXw, positionX + boxWidth);
        int yEnd = (int)Math.Min(clipYh, positionY + boxHeight);
        int index1 = frame.Offset + xStart*frame.Buffer.Height + yStart;
        for (int x1 = xStart; x1 < xEnd; ++x1, index1 += frame.Buffer.Height)
        {
            Array.Fill(frame.Buffer.Data, penColor, index1, Math.Max(0, yEnd - yStart));
        }
    }

    // draws a string onto the canvas at position (startX, startY)
    public void DrawString(string text, int startX, int startY, Font font = null)
    {
        font ??= Font9X16;
        int charactersPerRow = font.Texture.Width / font.CharacterWidth;
        int drawX = startX;
        int drawY = startY;

        // Loop through each character in the input string
        for (int i = 0; i < text.Length; i++)
        {
            byte charIndex = (byte)text[i]; // Get the ASCII value of the character (0-255)
            if (charIndex == 13 || charIndex == 10) { drawX = startX; drawY += font.CharacterHeight; continue; }

            // Calculate the row and column in the font texture
            int charRow = charIndex / charactersPerRow;
            int charColumn = charIndex % charactersPerRow;

            // Calculate the starting coordinates in the font texture
            int sourceX = charColumn * font.CharacterWidth;
            int sourceY = charRow * font.CharacterHeight;

            // Loop through the character's pixels
            for (int x = 0; x < font.CharacterWidth; x++)
                for (int y = 0; y < font.CharacterHeight; y++)
                {
                    uint pixel = font.Texture.Data[(sourceX + x) * font.Texture.Height + sourceY + y];
                    if (pixel == 0xFFFFFFFF) // white pixels (255, 255, 255) are used for the character and black for the background
                    {
                        SetPixel(drawX + x, drawY + y); // Draw the pixel using the current pen color, respecting the frame bounds
                    }
                }
                
            drawX += font.CharacterWidth; // Move to the next character position
        }
    }
}
