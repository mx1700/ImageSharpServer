using System.Globalization;
using SixLabors.ImageSharp.Web.Commands;
using SixLabors.ImageSharp.Web.Processors;

namespace ImageSharpServer;

public class GaussianSharpenProcessor: IImageWebProcessor
{
    public const string Sharpen = "sharpen";
    
    //TODO: v 表示版本，其实并不是用来处理 Processor，因为缓存 key 是通过 Commands 列表计算的，当图片被替换时，key 不变，导致一直读取缓存
    //这时可以通过增加 v=新值 来读取新图片
    private static readonly IEnumerable<string> SharpenCommands
        = [Sharpen, "v"];
    
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