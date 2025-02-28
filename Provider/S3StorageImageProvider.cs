using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp.Web.Providers;
using SixLabors.ImageSharp.Web.Providers.AWS;
using SixLabors.ImageSharp.Web.Resolvers;
using SixLabors.ImageSharp.Web.Resolvers.AWS;

namespace ImageSharpServer.Provider;

/// <summary>
/// Returns images stored in AWS S3.
///
/// SixLabors 内置的 S3StorageImageProvider 无法满足需求，因此需要自定义一个。
/// 因为 S3 的 ForcePathStyle 选项写死了 true，导致部分 S3 服务无法使用。
/// </summary>
public class S3StorageImageProvider : IImageProvider
{
    /// <summary>
    /// Character array to remove from paths.
    /// </summary>
    private static readonly char[] SlashChars = { '\\', '/' };

    /// <summary>
    /// The containers for the blob services.
    /// </summary>
    private readonly Dictionary<string, AmazonS3Client> buckets
        = new();

    private readonly S3StorageImageProviderOptions storageOptions;
    private Func<HttpContext, bool>? match;

    /// <summary>
    /// Contains various helper methods based on the current configuration.
    /// </summary>
    private readonly FormatUtilities formatUtilities;

    /// <summary>
    /// Initializes a new instance of the <see cref="AWSS3StorageImageProvider"/> class.
    /// </summary>
    /// <param name="storageOptions">The S3 storage options</param>
    /// <param name="formatUtilities">Contains various format helper methods based on the current configuration.</param>
    public S3StorageImageProvider(IOptions<S3StorageImageProviderOptions> storageOptions, FormatUtilities formatUtilities)
    {
        // Assert.NotNull(storageOptions, nameof(storageOptions));

        this.storageOptions = storageOptions.Value;

        this.formatUtilities = formatUtilities;

        foreach (S3BucketClientOptions bucket in this.storageOptions.S3Buckets)
        {
            this.buckets.Add(bucket.BucketName, CreateClient(bucket));
        }
    }

    /// <inheritdoc/>
    public ProcessingBehavior ProcessingBehavior { get; } = ProcessingBehavior.All;

    /// <inheritdoc />
    public Func<HttpContext, bool> Match
    {
        get => this.match ?? this.IsMatch;
        set => this.match = value;
    }

    /// <inheritdoc />
    public bool IsValidRequest(HttpContext context)
        => this.formatUtilities.TryGetExtensionFromUri(context.Request.GetDisplayUrl(), out _);

    /// <inheritdoc />
    public async Task<IImageResolver?> GetAsync(HttpContext context)
    {
        // Strip the leading slash and bucket name from the HTTP request path and treat
        // the remaining path string as the key.
        // Path has already been correctly parsed before here.
        string bucketName = string.Empty;
        IAmazonS3? s3Client = null;

        // We want an exact match here to ensure that bucket names starting with
        // the same prefix are not mixed up.
        string? path = context.Request.Path.Value?.TrimStart(SlashChars);

        if (path is null)
        {
            return null;
        }

        int index = path.IndexOfAny(SlashChars);
        string nameToMatch = index != -1 ? path.Substring(0, index) : path;

        foreach (string k in this.buckets.Keys)
        {
            if (nameToMatch.Equals(k, StringComparison.OrdinalIgnoreCase))
            {
                bucketName = k;
                s3Client = this.buckets[k];
                break;
            }
        }

        // Something has gone horribly wrong for this to happen but check anyway.
        if (s3Client is null)
        {
            return null;
        }

        // Key should be the remaining path string.
        string key = path.Substring(bucketName.Length).TrimStart(SlashChars);

        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        KeyExistsResult keyExists = await KeyExists(s3Client, bucketName, key);
        if (!keyExists.Exists)
        {
            return null;
        }

        return new AWSS3StorageImageResolver(s3Client, bucketName, key, keyExists.Metadata);
    }

    private bool IsMatch(HttpContext context)
    {
        // Only match loosly here for performance.
        // Path matching conflicts should be dealt with by configuration.
        string? path = context.Request.Path.Value?.TrimStart(SlashChars);

        if (path is null)
        {
            return false;
        }

        foreach (string bucket in this.buckets.Keys)
        {
            if (path.StartsWith(bucket, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    // ref https://github.com/aws/aws-sdk-net/blob/master/sdk/src/Services/S3/Custom/_bcl/IO/S3FileInfo.cs#L118
    private static async Task<KeyExistsResult> KeyExists(IAmazonS3 s3Client, string bucketName, string key)
    {
        try
        {
            GetObjectMetadataRequest request = new() { BucketName = bucketName, Key = key };

            // If the object doesn't exist then a "NotFound" will be thrown
            GetObjectMetadataResponse metadata = await s3Client.GetObjectMetadataAsync(request);
            return new KeyExistsResult(metadata);
        }
        catch (AmazonS3Exception e)
        {
            if (string.Equals(e.ErrorCode, "NoSuchBucket", StringComparison.Ordinal))
            {
                return default;
            }

            if (string.Equals(e.ErrorCode, "NotFound", StringComparison.Ordinal))
            {
                return default;
            }

            // If the object exists but the client is not authorized to access it, then a "Forbidden" will be thrown.
            if (string.Equals(e.ErrorCode, "Forbidden", StringComparison.Ordinal))
            {
                return default;
            }

            throw;
        }
    }

    private readonly record struct KeyExistsResult(GetObjectMetadataResponse Metadata)
    {
        public bool Exists => this.Metadata is not null;
    }
    
    private static AmazonS3Client CreateClient(S3BucketClientOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.Endpoint))
        {
            // AccessKey can be empty.
            // AccessSecret can be empty.
            // PathStyle endpoint doesn't support AccelerateEndpoint.
            AmazonS3Config config = new() { ServiceURL = options.Endpoint, ForcePathStyle = options.ForcePathStyle, AuthenticationRegion = options.Region };
            SetTimeout(config, options.Timeout);
            return new AmazonS3Client(options.AccessKey, options.AccessSecret, config);
        }
        else if (!string.IsNullOrWhiteSpace(options.AccessKey))
        {
            // AccessSecret can be empty.
            // Guard.NotNullOrWhiteSpace(options.Region, nameof(options.Region));
            RegionEndpoint region = RegionEndpoint.GetBySystemName(options.Region);
            AmazonS3Config config = new() { RegionEndpoint = region, UseAccelerateEndpoint = options.UseAccelerateEndpoint };
            SetTimeout(config, options.Timeout);
            return new AmazonS3Client(options.AccessKey, options.AccessSecret, config);
        }
        else if (!string.IsNullOrWhiteSpace(options.Region))
        {
            RegionEndpoint region = RegionEndpoint.GetBySystemName(options.Region);
            AmazonS3Config config = new() { RegionEndpoint = region, UseAccelerateEndpoint = options.UseAccelerateEndpoint };
            SetTimeout(config, options.Timeout);
            return new AmazonS3Client(config);
        }
        else
        {
            throw new ArgumentException("Invalid configuration.", nameof(options));
        }
    }
    
    private static void SetTimeout(ClientConfig config, TimeSpan? timeout)
    {
        // We don't want to override the default timeout if it's not set.
        if (timeout.HasValue)
        {
            config.Timeout = timeout.Value;
        }
    }
}
