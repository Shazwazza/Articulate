using System;
using System.Security.Cryptography;

using Microsoft.AspNetCore.DataProtection.Infrastructure;
using Umbraco.Cms.Core.Hosting;
using Umbraco.Extensions;

namespace Articulate.Routing
{
    /// <summary>
    /// Returns the route refresh key for the key value pair db table for this website instance.
    /// </summary>
    public sealed class CacheRefreshKey
    {
        private readonly IHostingEnvironment _hostingEnvironment;
        private readonly IApplicationDiscriminator _appDiscriminator;
        private readonly Lazy<string> _appKey;

        public CacheRefreshKey(
            IHostingEnvironment hostingEnvironment,
            IApplicationDiscriminator appDiscriminator)
        {
            _hostingEnvironment = hostingEnvironment;
            _appDiscriminator = appDiscriminator;

            _appKey = new Lazy<string>(() =>
            {
                // Most of this is borrowed from Umbraco core to get a unique key per website install.
                var appId = _appDiscriminator.Discriminator?.ReplaceNonAlphanumericChars(string.Empty) ?? string.Empty;
                var appPath = _hostingEnvironment.ApplicationPhysicalPath?.ToLowerInvariant() ?? string.Empty;
                var hash = (appId + ":::" + appPath).GenerateHash<SHA1>();
                return $"Articulate.CacheRefresh.{hash}";
            });
        }

        public string Key => _appKey.Value;
    }
}
