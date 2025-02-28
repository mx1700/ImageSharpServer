using System.Globalization;
using SixLabors.ImageSharp.Web.Commands;
using SixLabors.ImageSharp.Web.Processors;

namespace ImageSharpServer;

public class CustomResizeWebProcessor: ResizeWebProcessor, IImageWebProcessor
{ 
    FormattedImage IImageWebProcessor.Process(
        FormattedImage image,
        ILogger logger,
        CommandCollection commands,
        CommandParser parser,
        CultureInfo culture)
    {
        logger.LogInformation("CustomResizeWebProcessor.Process");
        var mode = commands.GetValueOrDefault(Mode);
        var width = commands.GetValueOrDefault(Width);
        var height = commands.GetValueOrDefault(Height);

        if (mode?.ToLower() == "maxmin")
        {
            commands[Mode] = "min";
            if (width != null && height != null)
            {
                var sourceWidth = image.Image.Width;
                var sourceHeight = image.Image.Height;
                
                var sourceRatio = (double)sourceWidth / sourceHeight;
                var targetRatio = double.Parse(width) / double.Parse(height);
                
                logger.LogInformation($"mode: {mode}, sourceWidth: {sourceWidth}, sourceHeight: {sourceHeight}");

                if (sourceRatio > targetRatio)
                {
                    commands[Width] = width;
                    commands[Height] = null!;
                }
                else
                {
                    commands[Height] = height;
                    commands[Width] = null!;
                }
            }
        }
        return base.Process(image, logger, commands, parser, culture);
    }
}