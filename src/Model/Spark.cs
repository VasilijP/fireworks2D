namespace Fireworks2D.Model;

// Represents a 'light' of a given color and intensity
public sealed class Spark(SparkType type)
{
    public const int TimeSteps = 16666 + 1000; // Number of time steps in microseconds based on 60Hz (minimum refresh rate) + some padding of 1ms
    public SparkType Type = type;
    public static double[][] VelocityDecayFactors;
    public static double[][] PositionDecayFactors;

    public double X;
    public double Y;
    
    //transformed coordinates
    public int Tx;
    public int Ty;

    //speed vector vx, vy
    public double Vx = 0.0;
    public double Vy = 0.0;
    
    // remaining time to explosion/burnout
    public double Time;

    // how many rounds left when new sparks are going to be created when this one is out of 'cas'? (could be 0 if none)
    public int SplitC;

    public uint Color;
    public uint FlareColor;
    
    public static void InitDecayFactors()
    {
        VelocityDecayFactors = new double[Enum.GetValues<SparkType>().Length][];
        PositionDecayFactors = new double[Enum.GetValues<SparkType>().Length][];
        
        foreach (SparkType t in Enum.GetValues<SparkType>())
        {
            VelocityDecayFactors[(int)t] = new double[TimeSteps+1];
            PositionDecayFactors[(int)t] = new double[TimeSteps+1];
            
            double k = GetDecayFactor(t); // decay constant(s) for a given mass
            
            for (int i = 0; i <= TimeSteps; i++)
            {
                int dtMicroseconds = i + 1;  // dt in microseconds
                double dt = dtMicroseconds * 0.000001;  // Convert to seconds
                double fv = Math.Exp(-k * dt);
                double fx = (1 - fv) / k;

                VelocityDecayFactors[(int)t][i] = fv;
                PositionDecayFactors[(int)t][i] = fx;
            }
        }
    }
    
    // Physical analogy of this is the particle (or ball) flying through the air.
    // We assume same size, shape and aerodynamic properties for all particles.
    // The only difference is the mass of the particle.
    // Original algorith expected a loss of speed for Standard spark of 15% per 1/60s
    // and for Flare of 2% per 1/60s.
    // I introduced a Sparkle (which is separated from Flare every 1/30s) which is very light, thus loses a lot of speed.
    private static double GetDecayFactor(SparkType type)
    {
        return type switch
        {
            SparkType.Sparkle  => 83.178,
            SparkType.Standard => 9.741,
            SparkType.Flare    => 1.207,
            _ => 9.741
        };
    }

    // mass could be used in combination with 'air viscosity' to simulate heavier and lighter particles (floating vs falling)
    public static double GetMass(SparkType type)
    {
        return type switch
        {
            SparkType.Sparkle => 0.2,
            SparkType.Standard => 1.0,
            SparkType.Flare => 7.5,
            _ => 1.0
        };
    }
}
