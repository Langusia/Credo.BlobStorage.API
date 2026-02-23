using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Credo.BlobStorage.Client.DependencyInjection
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddBlobStorageClient(
            this IServiceCollection services,
            Action<BlobStorageClientOptions> configure)
        {
            services.Configure(configure);

            services.AddHttpClient<IBlobStorageClient, BlobStorageClient>()
                .ConfigureHttpClient((sp, client) =>
                {
                    var options = sp.GetRequiredService<IOptions<BlobStorageClientOptions>>().Value;
                    var baseUrl = options.BaseUrl.TrimEnd('/') + "/";
                    client.BaseAddress = new Uri(baseUrl);
                    client.Timeout = options.Timeout;
                });

            return services;
        }
    }
}
