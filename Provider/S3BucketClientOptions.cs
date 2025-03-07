﻿using SixLabors.ImageSharp.Web.Providers.AWS;

namespace ImageSharpServer.Provider;

public class S3StorageImageProviderOptions
{
    /// <summary>
    /// Gets or sets the collection of blob container client options.
    /// </summary>
    public ICollection<S3BucketClientOptions> S3Buckets { get; set; } = new HashSet<S3BucketClientOptions>();
}

public class S3BucketClientOptions: AWSS3BucketClientOptions
{
    public bool ForcePathStyle { get; set; } = false;
    public string[] Prefix { get; set; } = [];
}