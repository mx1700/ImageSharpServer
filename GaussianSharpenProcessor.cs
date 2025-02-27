using System.Globalization;
using SixLabors.ImageSharp.Web.Commands;
using SixLabors.ImageSharp.Web.Processors;

namespace ImageSharpServer;

public class GaussianSharpenProcessor: IImageWebProcessor
{
    public const string Sharpen = "sharpen";
    
    private static readonly IEnumerable<string> SharpenCommands
        = [Sharpen];
    
    public FormattedImage Process(FormattedImage image, ILogger logger, CommandCollection commands, CommandParser parser,
        CultureInfo culture)
    {
        logger.LogInformation("GaussianSharpenProcessor.Process");
        var sigmaStr = commands.GetValueOrDefault(Sharpen);
        if(sigmaStr != null && float.TryParse(sigmaStr, out var sigma))
        {
            image.Image.Mutate(x => x.GaussianSharpen(sigma));
        }
        return image;
    }

    public bool RequiresTrueColorPixelFormat(CommandCollection commands, CommandParser parser, CultureInfo culture)
    {
        return true;
    }

    public IEnumerable<string> Commands => SharpenCommands;
}