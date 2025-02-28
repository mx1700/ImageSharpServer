using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp.Web.Providers;

namespace ImageSharpServer.Provider;

public class CustomFileSystemProvider: FileProviderImageProvider
{
    public sealed override Func<HttpContext, bool> Match { get; set; }
    private CustomFileSystemProviderOptions Options { get; }
    
    private static readonly char[] SlashChars = ['\\', '/'];
    
    private PhysicalFileProvider FileProvider;
    
    // public CustomFileSystemProvider(
    //     IOptions<CustomFileSystemProviderOptions> options,
    //     IWebHostEnvironment environment,
    //     FormatUtilities formatUtilities)
    //     : base(GetProvider(options, environment), options.Value.ProcessingBehavior, formatUtilities)
    // {
    //     Options = options.Value;
    //     Match = _Match;
    // }
    
    public CustomFileSystemProvider(
        IOptions<CustomFileSystemProviderOptions> options,
        PhysicalFileProvider fileProvider,
        FormatUtilities formatUtilities)
        : base(fileProvider, options.Value.ProcessingBehavior, formatUtilities)
    {
        Options = options.Value;
        Match = _Match;
        FileProvider = fileProvider;
    }
    
    public static CustomFileSystemProvider Make(
        IOptions<CustomFileSystemProviderOptions> options,
        IWebHostEnvironment environment, FormatUtilities formatUtilities)
    {
        var fileProvider = GetProvider(options, environment);
        return new(options, fileProvider, formatUtilities);
    }
    
    private static string GetProviderRoot(CustomFileSystemProviderOptions options, string webRootPath, string contentRootPath)
    {
        string providerRootPath = options.ProviderRootPath ?? webRootPath;
        if (string.IsNullOrEmpty(providerRootPath))
        {
            throw new InvalidOperationException("The provider root path cannot be determined, make sure it's explicitly configured or the webroot is set.");
        }

        if (!Path.IsPathFullyQualified(providerRootPath))
        {
            // Ensure this is an absolute path (resolved to the content root path)
            providerRootPath = Path.GetFullPath(providerRootPath, contentRootPath);
        }

        return EnsureTrailingSlash(providerRootPath);
    }

    public static PhysicalFileProvider GetProvider(
        IOptions<CustomFileSystemProviderOptions> options,
        IWebHostEnvironment environment)
    {
        // Guard.NotNull(options, nameof(options));
        // Guard.NotNull(environment, nameof(environment));
        return new(GetProviderRoot(options.Value, environment.WebRootPath, environment.ContentRootPath));
    }
    
    static string EnsureTrailingSlash(string path)
    {
        if (!string.IsNullOrEmpty(path) &&
            path[^1] != Path.DirectorySeparatorChar)
        {
            return path + Path.DirectorySeparatorChar;
        }

        return path;
    }

    private bool _Match(HttpContext context)
    {
        if (Options.MatchType == FileSystemProviderMatchType.Always)
        {
            return true;
        }

        var path = context.Request.Path.Value?.TrimStart(SlashChars);

        if (path is null)
        {
            return false;
        }
        
        var match = true;
        
        if ((Options.MatchType & FileSystemProviderMatchType.Prefix) == FileSystemProviderMatchType.Prefix && Options.Prefix is not null)
        {
            var prefixMatch = Options.Prefix.Any(p => path.StartsWith(p.TrimStart(SlashChars), StringComparison.OrdinalIgnoreCase));
            match = match && prefixMatch;
        }

        if ((Options.MatchType & FileSystemProviderMatchType.FileExists) == FileSystemProviderMatchType.FileExists)
        {
            var fileInfo = FileProvider.GetFileInfo(path);
            match = match && fileInfo.Exists;
        }

        return match;
    }
}