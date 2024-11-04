using Fireworks2D.Util;

namespace Fireworks2D.Presentation;

public sealed class FrameDescriptor
{
    public readonly int FrameIndex;
    public readonly int Offset;
    public readonly FrameBuffer Buffer;
    public readonly Canvas Canvas;

    internal FrameDescriptor(int frameIndex, int offset, FrameBuffer buffer)
    {
        FrameIndex = frameIndex;
        Offset = offset;
        Buffer = buffer;
        Canvas = new Canvas(this);
    }
}
