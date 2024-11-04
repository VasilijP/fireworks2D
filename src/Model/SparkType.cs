namespace Fireworks2D.Model;

// Standard spark decelerates at the rate of 15% per 1/60s
// Flare spark decelerates just at the rate of 2% per 1/60s (having mass ~ 7.5x of standard spark)
public enum SparkType
{
    Standard, // 1.0
    Flare,    // 7.5
    Sparkle,  // 0.2
}
