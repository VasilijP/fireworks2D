using CommandLine;

namespace Fireworks2D.Configuration;

[Verb("fire", isDefault: true, HelpText = "Run the 2D fireworks.")]
public class FireworkOptions : CommonOptions
{
}
