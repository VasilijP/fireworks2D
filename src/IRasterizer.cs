using Fireworks2D.Presentation;

namespace Fireworks2D;

// Implementation of this is required to produce current image of the world/scene.  
public interface IRasterizer
{
    public void Render(FrameBuffer buffer, double secondsSinceLastFrame);
}
