using ImageSharpServer;
using SixLabors.ImageSharp.Web.Caching;
using SixLabors.ImageSharp.Web.DependencyInjection;
using SixLabors.ImageSharp.Web.Processors;
using SixLabors.ImageSharp.Web.Providers;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddImageSharp()
    .RemoveProcessor<ResizeWebProcessor>()
    .AddProcessor<CustomResizeWebProcessor>()
    .AddProcessor<GaussianSharpenProcessor>()
    .Configure<PhysicalFileSystemProviderOptions>(options =>
    {
        options.ProviderRootPath = builder.Configuration["IMAGES_ROOT_PATH"] ?? Path.Combine(builder.Environment.ContentRootPath, "images");
    })
    .Configure<PhysicalFileSystemCacheOptions>(options =>
    {
        options.CacheRootPath = builder.Configuration["CACHE_ROOT_PATH"] ?? Path.Combine(builder.Environment.ContentRootPath, "cache");
        options.CacheFolderDepth = 2;
        options.CacheFolder = "./";
    });

var app = builder.Build();

app.UseImageSharp();

app.Run();
