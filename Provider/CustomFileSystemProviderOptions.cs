using SixLabors.ImageSharp.Web.Providers;

namespace ImageSharpServer.Provider;

public class CustomFileSystemProviderOptions: PhysicalFileSystemProviderOptions
{
    public FileSystemProviderMatchType MatchType { get; set; } = FileSystemProviderMatchType.Always;
    public string[] Prefix { get; set; } = [];
}

[Flags]
public enum FileSystemProviderMatchType
{
    Always = 0,
    Prefix = 1,
    FileExists = 2,
}