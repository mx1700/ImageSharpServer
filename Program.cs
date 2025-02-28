using ImageSharpServer;
using ImageSharpServer.Provider;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp.Web.Caching;
using SixLabors.ImageSharp.Web.DependencyInjection;
using SixLabors.ImageSharp.Web.Processors;
using SixLabors.ImageSharp.Web.Providers;

var builder = WebApplication.CreateBuilder(args);

var imageSharpBuilder = builder.Services.AddImageSharp()
    .RemoveProcessor<ResizeWebProcessor>()
    .AddProcessor<CustomResizeWebProcessor>()
    .AddProcessor<GaussianSharpenProcessor>()
    .Configure<PhysicalFileSystemCacheOptions>(options =>
    {
        options.CacheRootPath = builder.Configuration["CACHE_ROOT_PATH"] ?? Path.Combine(builder.Environment.ContentRootPath, "cache");
        options.CacheFolderDepth = 2;
        options.CacheFolder = "./";
    });

imageSharpBuilder.RemoveProvider<PhysicalFileSystemProvider>();

if (builder.Configuration["FILE_ROOT_PATH"] != null)
{
    imageSharpBuilder.Configure<CustomFileSystemProviderOptions>(options =>
    {
        options.ProviderRootPath = builder.Configuration["FILE_ROOT_PATH"] ??
                                   Path.Combine(builder.Environment.ContentRootPath, "images");
        options.MatchType = builder.Configuration["FILE_MATCH_TYPE"] switch
        {
            "Prefix" => FileSystemProviderMatchType.Prefix,
            "FileExists" => FileSystemProviderMatchType.FileExists,
            "Prefix|FileExists" => FileSystemProviderMatchType.Prefix | FileSystemProviderMatchType.FileExists,
            _ => FileSystemProviderMatchType.Always
        };
        options.Prefix = builder.Configuration["FILE_MATCH_PREFIX"];
        options.ProcessingBehavior = ProcessingBehavior.All;
    });
    imageSharpBuilder.AddProvider<CustomFileSystemProvider>(service =>
    {
        var options = service.GetService<IOptions<CustomFileSystemProviderOptions>>()!;
        var fileProvider = CustomFileSystemProvider.GetProvider(options, builder.Environment);
        return new CustomFileSystemProvider(options, fileProvider, service.GetService<FormatUtilities>()!);
    });
}

if (builder.Configuration["S3_BUCKET_NAME"] != null)
{
    imageSharpBuilder.Configure<S3StorageImageProviderOptions>(options =>
        {
            options.S3Buckets.Add(new S3BucketClientOptions
            {
                Endpoint = builder.Configuration["S3_ENDPOINT"],
                BucketName = builder.Configuration["S3_BUCKET_NAME"]!,
                AccessKey = builder.Configuration["S3_ACCESS_KEY"],
                AccessSecret = builder.Configuration["S3_ACCESS_SECRET"],
                Region = builder.Configuration["S3_REGION"],
                Timeout = builder.Configuration["S3_TIMEOUT"] != null
                    ? TimeSpan.FromSeconds(int.Parse(builder.Configuration["S3_TIMEOUT"]!))
                    : TimeSpan.FromSeconds(5),
                ForcePathStyle = builder.Configuration["S3_FORCE_PATH_STYLE"] == "true",
            });
        })
        .AddProvider<S3StorageImageProvider>();
}

var app = builder.Build();

app.UseImageSharp();

app.Run();
