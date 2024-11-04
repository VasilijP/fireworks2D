using CommandLine;
using Fireworks2D.Configuration;
using Fireworks2D.Presentation;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;

namespace Fireworks2D;

public static class Program
{
    public static int Main(string[] args)
    {
        return Parser.Default.ParseArguments<FireworkOptions>(args).MapResult(opts => RunWithOptions(opts, new Fireworks2dRasterizer(opts)), errs => 1);
    }

    private static int RunWithOptions<T>(T config, IRasterizer ras) where T : CommonOptions
    {
        NativeWindowSettings nativeWindowSettings = new()
        {
            Title = "Fireworks 2D",
            WindowState = config.Fullscreen?WindowState.Fullscreen:WindowState.Normal,
            Vsync = config.VSync?VSyncMode.On:VSyncMode.Off,
            ClientSize = new Vector2i(config.Width, config.Height),
            Flags = ContextFlags.ForwardCompatible, // This is needed to run on macos
        };
        
        using EngineWindow window = new(config, ras, GameWindowSettings.Default, nativeWindowSettings);
        window.Run();
        return 0;
    }
}
