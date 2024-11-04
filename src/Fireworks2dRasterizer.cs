using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using Fireworks2D.Configuration;
using Fireworks2D.Controls;
using Fireworks2D.Model;
using Fireworks2D.Presentation;
using Fireworks2D.Util;

namespace Fireworks2D;

public class Fireworks2dRasterizer : IRasterizer
{
    private const int MaxSparks = 1000000;
    
    private readonly FrameChart frameChart = new(8, 0, 120, Func.EncodePixelColor(0,0xFF,0), FrameBuffer.Fps);
    
    private readonly Control mouseDx;
    private readonly Control mouseDy;
    private readonly Control mouseButtonLeft;
    
    private readonly ControlParam<double> mainLaunchSeconds = ControlParamRegistry.Get("Time2MainLaunchSeconds", 0.25); // seconds - time between new main sparks are generated
    
    private readonly ControlParam<double> burstSeconds =  ControlParamRegistry.Get("Time2BurstSeconds", 0.35); // seconds - time to generate new sparks
    
    private readonly ControlParam<double> burstSecondsMod = ControlParamRegistry.Get("VarianceTime2Burst", 0.45); // additional variance of burstSeconds
    
    private readonly ControlParam<double> gravity = ControlParamRegistry.Get("GravitationalAccelerationConstant", 69.80665);  // increment of speed in y axis per second

    private readonly ControlParam<int> newSparkCount = ControlParamRegistry.Get("NewSparkCount", 15);  // number of new sparks at explosion
    
    private readonly ControlParam<int> stages = ControlParamRegistry.Get("BurstStageCount", 3); // number of stages of explosion
    
    private readonly ControlParam<bool> drawSparkFlare = ControlParamRegistry.Get("DrawSparkFlare", true); // draw spark flare
    
    private readonly ControlParam<bool> drawFrameChart = ControlParamRegistry.Get("DrawFrameChart", true); // draw frametime chart
    
    private readonly ControlParam<ColorSet> sparkColors = ControlParamRegistry.Get("ColorSet", UnicornColorSet);
    
    private readonly ControlParam<double> vectorFadeStep = ControlParamRegistry.Get("VectorFadeStep", VectorFadeStep); // fade step (only for vectorized fading path)
    
    private readonly ParamGroup paramGroup = new(10, 200, 400);
    
    private double startCas = 0.0; // seconds - time remaining to main spark generation -> periodically reset to 'mainLaunchSeconds'

    private int mx = 0;
    private int my = 0;
    private readonly int sceneWidth;
    private readonly int sceneHeight;
    private readonly uint[] scenePixel; // RGBAx8bit
    private readonly Spark[] sparks = new Spark[MaxSparks];
    private int sparkCount = 0;
    private readonly uint[] fadeTable;
    private const double FrequencyHz = 60; // Set the frequency as 60 Hz, based on old 60Hz expectation -> 16.66ms per simulation step
    private const double IntervalDuration = 1.0 / FrequencyHz; // maximum simulation step duration (usually it goes faster than this)
    private const double VectorFadeStep = 1.0 / 480; // we fade by 1 per 2.08333333333333333333333333333 ms (1/480s), calculated from original 8 steps per 1/60s
    private static readonly List<Tuple<int, int>> FlareOffsetTuples = [new Tuple<int, int>(-1, 0), new Tuple<int, int>(0, 1), new Tuple<int, int>(0, -1), new Tuple<int, int>(1, 0)]; // flare on: left, top, bottom, right (in this order)
    private readonly int[] flareOffset = new int[FlareOffsetTuples.Count];
    
    // list of base spark colors to choose from (for new sparks)
    private static readonly ColorSet StandardColorSet = new() { Name = "Standard RGB+CMY+W", Colors = [ Func.EncodePixelColor(0xFF, 0, 0), Func.EncodePixelColor(0, 0xFF, 0), Func.EncodePixelColor(0, 0, 0xFF), Func.EncodePixelColor(0xFF, 0xFF, 0), 
                                                                                                        Func.EncodePixelColor(0xFF, 0, 0xFF), Func.EncodePixelColor(0, 0xFF, 0xFF), Func.EncodePixelColor(0xFF, 0xFF, 0xFF) ]};
      
    private static readonly ColorSet UnicornColorSet = new() { Name = "Unicorn colors", Colors = [ Func.EncodePixelColor(255, 192, 203), Func.EncodePixelColor(255, 105, 180), Func.EncodePixelColor(218, 112, 214), Func.EncodePixelColor(75, 10, 130), 
                                                                                                   Func.EncodePixelColor(240, 230, 140), Func.EncodePixelColor(10, 128, 10), Func.EncodePixelColor(255, 235, 10) ] };

    private readonly Random rnd = new();

    public Fireworks2dRasterizer(FireworkOptions opts)
    {
        sceneWidth = opts.Width;
        sceneHeight = opts.Height;
        scenePixel = new uint[sceneWidth * sceneHeight]; // RGBAx8bit
        drawFrameChart.Value = opts.Dofps;
        mouseDx = opts.MouseControls[ControlEnum.MOUSE_DELTA_X];
        mouseDy = opts.MouseControls[ControlEnum.MOUSE_DELTA_Y];
        mouseButtonLeft = opts.MouseControls[ControlEnum.MOUSE_BUTTON_LEFT];
        
        // control params
        //TODO: (?) hide controls if no mouse movement is detected
        paramGroup.AddParam("FPS Chart", drawFrameChart);
        paramGroup.AddParam("Main Launch Delay", mainLaunchSeconds, 0.001, 3.0);
        paramGroup.AddParam("Burst Delay", burstSeconds, 0.001, 2.0);
        paramGroup.AddParam("Burst Delay Variance", burstSecondsMod, 0.001, 2.0);
        paramGroup.AddParam("Gravity", gravity, 1.0, 300.0);
        paramGroup.AddParam("New Sparks", newSparkCount, 1, 100);
        paramGroup.AddParam("Stages", stages, 1, 5);
        if (Avx512BW.IsSupported) { paramGroup.AddParam("Vector Fade Step", vectorFadeStep, 0.000333, 0.01); }
        paramGroup.AddParam("Draw Flare", drawSparkFlare);
        paramGroup.AddParam("Color Set", sparkColors, [UnicornColorSet, StandardColorSet]);
        
        // lookup table for fading sparks (used only if no avx512 is available)
        if (!Avx512BW.IsSupported)
        {
            fadeTable = new uint[256*256*256];
            for (int i = 0; i < fadeTable.Length; ++i)
            {
                Func.DecodePixelColor((uint)i, out int r, out int g, out int b);
                r = r > 8? r - 8 : 0;
                g = g > 8? g - 8 : 0;
                b = b > 8? b - 8 : 0;
                fadeTable[i] = Func.EncodePixelColor(r, g, b);
            }
        }
        
        Spark.InitDecayFactors();

        // avoid new-ing sparks, reuse them all the time
        for (int i = 0; i < MaxSparks; ++i) { sparks[i] = new Spark(SparkType.Standard); }
        
        // Offsets calculated according to buffer size
        for (int i = 0; i < FlareOffsetTuples.Count; ++i)
        {
            flareOffset[i] = FlareOffsetTuples[i].Item1 * sceneHeight + FlareOffsetTuples[i].Item2; // X * H + Y (buffer is organized by columns)
        }
    }

    public void Render(FrameBuffer buffer, double secondsSinceLastFrame)
    {
        FrameDescriptor frame = buffer.StartNextFrame();
        FadeScenePixels(scenePixel, secondsSinceLastFrame);
        
        mx = Math.Clamp(mx + Interlocked.Exchange(ref mouseDx.Delta, 0), 0, sceneWidth-1);
        my = Math.Clamp(my + Interlocked.Exchange(ref mouseDy.Delta, 0), 0, sceneHeight-1);
        if (mouseButtonLeft.Active) { paramGroup.Click(mx, my); }
        
        double timePassed = secondsSinceLastFrame;
        while (timePassed > IntervalDuration) // we do max 1/FrequencyHz simulation step at a time
        {
            TimeProgression(IntervalDuration);
            timePassed -= IntervalDuration;
        }
        TimeProgression(timePassed);
        
        Array.Copy(scenePixel, 0, frame.Buffer.Data, frame.Offset, scenePixel.Length);
        
        Canvas canvas = frame.Canvas;
        paramGroup.Draw(canvas, mx, my);
        canvas.SetPenColor(0xFF, 0xFF, 0xFF).DrawString($"Particle count: {sparkCount}, FPS (average): {FrametimeComponent.FrametimeToFps(FrameBuffer.Fps.GetAverageFrametime()):F2}", 10, 10, Canvas.Font9X16);
        canvas.DrawString($"Press ESC to Exit.", 10, sceneHeight - 20, Canvas.Font16X16);
        if (drawFrameChart.Value) { frameChart.Draw(canvas); }
        
        canvas.SetPenColor(0xFF, 0xFF, 0).MoveTo(mx, my).Draw(0, 20, true).Draw(10, 0, true).Draw(-10, -20, true);
        
        buffer.FinishFrame(frame);
    }

    private void SwapOutParticle(int index)
    {   
        --sparkCount;
        sparks[index].Color = sparks[sparkCount].Color;
        sparks[index].FlareColor = sparks[sparkCount].FlareColor;
        sparks[index].X = sparks[sparkCount].X;
        sparks[index].Y = sparks[sparkCount].Y;
        sparks[index].Vx = sparks[sparkCount].Vx;
        sparks[index].Vy = sparks[sparkCount].Vy;
        sparks[index].SplitC = sparks[sparkCount].SplitC;
        sparks[index].Time = sparks[sparkCount].Time;
        sparks[index].Type = sparks[sparkCount].Type;
    }

    private void GenerateNew(double ax, double ay, double avx, double avy, int uroven, uint farba)
    {
        double step = 2*Math.PI / newSparkCount.Value;
        double angle = 0.0;
        for (int i = Math.Clamp(MaxSparks - sparkCount - newSparkCount.Value, 0, newSparkCount.Value); i > 0; --i)
        {
            sparks[sparkCount].Color = Func.Darker(farba, 0.9);
            sparks[sparkCount].FlareColor = Func.Darker(farba, 0.15);
            sparks[sparkCount].X = ax;
            sparks[sparkCount].Y = ay;
            sparks[sparkCount].Vx = 60* (2 * (avx / 3 + Math.Cos(angle) * (4 + rnd.Next(4))));
            sparks[sparkCount].Vy = 60* (2 * (avy / 5 + Math.Sin(angle) * (4 + rnd.Next(4))));
            sparks[sparkCount].SplitC = uroven;
            sparks[sparkCount].Time = burstSeconds.Value + rnd.NextDouble()*burstSecondsMod.Value;
            sparks[sparkCount].Type = SparkType.Standard;
            ++sparkCount;
            angle += step;
        }
    }        

    private void TimeProgression(double timePassed)
    {
        // figure out whether to and fire up new rocket
        startCas -= timePassed;
        if (startCas < 0)
        {
            if (sparkCount + newSparkCount.Value < MaxSparks) // there is place for a new batch of sp_ii sparks (that are created after main explosion)
            {
                startCas += mainLaunchSeconds.Value; // reset countdown
                sparks[sparkCount].Color = sparkColors.Value.Colors[rnd.Next(sparkColors.Value.Colors.Length)];
                sparks[sparkCount].FlareColor = Func.Darker(sparks[sparkCount].Color, 0.15);
                sparks[sparkCount].X = rnd.Next(sceneWidth);
                sparks[sparkCount].Y = sceneHeight - 1;
                sparks[sparkCount].Vx = 60*(rnd.Next(8*Math.Max(1, sceneWidth/320)) - 4*Math.Max(1, sceneWidth/320));
                sparks[sparkCount].Vy = 60*(-20*Math.Max(1, sceneHeight/200) - rnd.Next(5*Math.Max(1, sceneHeight/200)));
                sparks[sparkCount].SplitC = stages.Value; // N generations of sparks (children will have rozd -= 1)
                sparks[sparkCount].Time = burstSeconds.Value + rnd.NextDouble()*burstSecondsMod.Value;
                sparks[sparkCount].Type = SparkType.Standard;
                
                if (rnd.Next(5) == 0) // this is hardcoded path to create flare instead of firework
                {
                    sparks[sparkCount].Time = 1.0/30; // every 1/30s a flare will separate a spark
                    sparks[sparkCount].Vx = 60*(rnd.Next(10*Math.Max(1, sceneWidth/320)) - 4*Math.Max(1, sceneWidth/320));
                    sparks[sparkCount].Vy = 60*(-3*Math.Max(1, sceneHeight/200) - rnd.Next(2*Math.Max(1, sceneHeight/200)));
                    sparks[sparkCount].Type = SparkType.Flare;
                    sparks[sparkCount].SplitC = 0;
                }

                ++sparkCount;
            }
        }
        
        //for (int sparkIndex = 0; sparkIndex < sparkCount; ++sparkIndex) // draw sparks directly here
        Parallel.For(0, sparkCount, (sparkIndex) =>
        {
            Spark particle = sparks[sparkIndex];

            particle.Tx = (int)particle.X;
            particle.Ty = (int)particle.Y;

            if (particle.Ty < 1 || particle.Ty > sceneHeight - 2 || particle.Tx < 1 || particle.Tx > sceneWidth - 2) { return; }
            int pos = sceneHeight * particle.Tx + particle.Ty;
            MixColors(ref scenePixel[pos], particle.Color);
            if (drawSparkFlare.Value) { foreach (int offset in flareOffset) { MixColors(ref scenePixel[pos + offset], particle.FlareColor); } }
        });

        // move existing sparks
        int timePassedInt = (int)(timePassed * 1e6);
        int index = sparkCount;
        while (index-- > 0)
        {
            Spark particle = sparks[index];
            
            // Update position
            particle.X += particle.Vx * Spark.PositionDecayFactors[(int)particle.Type][timePassedInt];
            particle.Y += particle.Vy * Spark.PositionDecayFactors[(int)particle.Type][timePassedInt];
            
            // Update velocities
            particle.Vx *= Spark.VelocityDecayFactors[(int)particle.Type][timePassedInt];
            particle.Vy *= Spark.VelocityDecayFactors[(int)particle.Type][timePassedInt];
            particle.Vy += gravity.Value * timePassed;
            particle.Time -= timePassed;

            if (particle.Time <= 0)
            {
                if (particle.Type == SparkType.Flare)
                {
                    particle.Time = 1.0/10;
                    if (sparkCount < MaxSparks)
                    {
                        sparks[sparkCount].Color = Func.Darker(particle.Color, 0.35);
                        sparks[sparkCount].FlareColor = Func.Darker(particle.Color, 0.15);
                        sparks[sparkCount].X = particle.X;
                        sparks[sparkCount].Y = particle.Y;
                        sparks[sparkCount].Vx = 120*(rnd.Next(5) - 2);
                        sparks[sparkCount].Vy = 120*(rnd.Next(5) - 2);
                        sparks[sparkCount].SplitC = 0;
                        sparks[sparkCount].Type = SparkType.Sparkle;
                        sparks[sparkCount].Time = burstSeconds.Value + rnd.NextDouble()*burstSecondsMod.Value;
                        ++sparkCount;
                    }
                }
                else if (particle.SplitC > 0) { GenerateNew(particle.X, particle.Y, particle.Vx, particle.Vy, particle.SplitC-1, particle.Color); }
            }
        }
            
        index = 0;
        while (index < sparkCount)
        {
            Spark particle = sparks[index];
            if (particle.X < 0 || particle.X > sceneWidth || particle.Time < 0 || particle.Y > sceneHeight) { SwapOutParticle(index); } // this overwrites the current particle, so we have to do this after generateNew params are read
            else { ++index; } // move to next particle only if we did not remove this one
        }
    }
    
    // more performant version to mix colors per (8bit) channel
    private static void MixColors(ref uint target, uint color)
    {
        // if (target == 0xFFFFFF) { return; }
        if (Ssse3.IsSupported)
        {
            Vector128<uint> targetVectorUInt32 = Vector128.CreateScalar(target); // we are using just 24bits effectively, but it is still worth the performance increase
            Vector128<uint> colorVectorUInt32 = Vector128.CreateScalar(color);
            
            Vector128<byte> targetVector = targetVectorUInt32.AsByte();
            Vector128<byte> colorVector = colorVectorUInt32.AsByte();
            
            Vector128<byte> resultVector = Sse2.AddSaturate(targetVector, colorVector);
            
            target = resultVector.AsUInt32().ToScalar();
        }
        else
        {
            Func.DecodePixelColor(color, out int red, out int green, out int blue);
            Func.DecodePixelColor(target, out int pRed, out int pGreen, out int pBlue);
            target = Func.EncodePixelColor(Math.Min(255, red + pRed), Math.Min(255, green + pGreen), Math.Min(255, blue + pBlue));
        }
    }
    
    private double fadeTime = 0.0; // seconds - time between fading steps are executed, fading works with smallest steps (1 shade) with vectorization or by 8 per 1/60s if not supported
    private const int VectorSize = 64; // AVX-512 processes 64 bytes at a time
    private void FadeScenePixels(uint[] pixels, double timePassed)
    {
        fadeTime -= timePassed;
        if (!Avx512BW.IsSupported)
        {
            if (fadeTime > 0) { return; }
            fadeTime += IntervalDuration; // carry out step of fading (60fps for original speed did 8 steps per 1/60s)
                
            // fade down existing lights, original palette was based on steps of 8 (of 256 == 8bit)
            // note(2024): original fading speed seems to be 8 of 256 levels per 1/60s so about 480 steps per 1s (480/s)
            unsafe 
            {
                fixed (uint* pScenePixel = pixels)
                {
                    uint* pEnd = pScenePixel + pixels.Length;
                    for (uint* p = pScenePixel; p != pEnd; ++p) { *p = fadeTable[*p]; }
                }
            }
            return;
        }
        
        int totalBytes = scenePixel.Length * sizeof(uint);
        int fadeValue = 0;
        while (fadeTime + vectorFadeStep.Value <= 0) { fadeTime += vectorFadeStep.Value; fadeValue += 1; }
        if (fadeValue == 0) return;
        
        Vector512<byte> fadeVector = Vector512.Create((byte)fadeValue); // vector filled with the fade value
        unsafe
        {
            fixed (uint* pScenePixel = scenePixel)
            {
                byte* p = (byte*)pScenePixel;
                
                for (int i = 0; i + VectorSize <= totalBytes; i += VectorSize)
                {
                    Vector512<byte> pixelVector = Avx512BW.LoadVector512(p + i); // load
                    pixelVector = Avx512BW.SubtractSaturate(pixelVector, fadeVector); // -fadeValue with clampping
                    Avx512BW.Store(p + i, pixelVector);
                }

                // note: for resolutions where Width*Height*4 is not divisible by 64, we ignore any remaining bytes/pixels
                // for (; i < totalBytes; i++) { p[i] = (byte)Math.Max(p[i] - 1, 0); } // <- this would do it but it is not needed for standard FHD or 4k...
            }
        }
    }
}
