using System;

namespace Credo.BlobStorage.Client
{
    public class BlobStorageClientOptions
    {
        public string BaseUrl { get; set; } = string.Empty;
        public bool AutoCreateBuckets { get; set; } = true;
        public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(5);
    }
}
