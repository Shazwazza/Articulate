#nullable enable
using System.Collections.Frozen;
using System.Net;
using System.Net.Sockets;
using FileSignatures;
using FileSignatures.Formats;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Api.Management.Routing;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Configuration.Models;
using Umbraco.Cms.Core.IO;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Strings;

namespace Articulate.Services
{
    /// <summary>
    /// Service for importing media into Articulate.
    /// </summary>
    public sealed class ArticulateImportMediaService : IArticulateImportMediaService
    {
        private static readonly HttpRequestOptionsKey<IPAddress> _pinnedAddressOption =
            new("ArticulatePinnedAddress");
        private const int MaxRedirects = 5;
        private static readonly FrozenSet<string> _fallbackAllowedExtensions =
            FrozenSet.ToFrozenSet([".png", ".jpg", ".jpeg", ".gif"]);

        private readonly ILogger<ArticulateImportMediaService> _logger;
        private readonly IMediaService _mediaService;
        private readonly MediaFileManager _mediaFileManager;
        private readonly IShortStringHelper _shortStringHelper;
        private readonly MediaUrlGeneratorCollection _mediaUrlGenerators;
        private readonly IContentTypeBaseServiceProvider _contentTypeBaseServiceProvider;
        private readonly IAbsoluteUrlBuilder _absoluteUrlBuilder;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IOptionsMonitor<ContentSettings> _contentSettings;
        private readonly IOptionsMonitor<RuntimeSettings> _runtimeSettings;
        private readonly IOptionsMonitor<Options.ArticulateOptions> _articulateOptions;
        private readonly IFileFormatInspector _fileFormatInspector;
        private readonly Lazy<IMedia> _articulateMediaFolder;

        public ArticulateImportMediaService(
            ILogger<ArticulateImportMediaService> logger,
            IMediaService mediaService,
            MediaFileManager mediaFileManager,
            IShortStringHelper shortStringHelper,
            MediaUrlGeneratorCollection mediaUrlGenerators,
            IContentTypeBaseServiceProvider contentTypeBaseServiceProvider,
            IAbsoluteUrlBuilder absoluteUrlBuilder,
            IHttpClientFactory httpClientFactory,
            IFileFormatInspector fileFormatInspector,
            IOptionsMonitor<ContentSettings> contentSettings,
            IOptionsMonitor<RuntimeSettings> runtimeSettings,
            IOptionsMonitor<Options.ArticulateOptions> articulateOptions)
        {
            _logger = logger;
            _mediaService = mediaService;
            _mediaFileManager = mediaFileManager;
            _shortStringHelper = shortStringHelper;
            _mediaUrlGenerators = mediaUrlGenerators;
            _contentTypeBaseServiceProvider = contentTypeBaseServiceProvider;
            _absoluteUrlBuilder = absoluteUrlBuilder;
            _httpClientFactory = httpClientFactory;
            _fileFormatInspector = fileFormatInspector;
            _contentSettings = contentSettings;
            _runtimeSettings = runtimeSettings;
            _articulateOptions = articulateOptions;

            _articulateMediaFolder = new Lazy<IMedia>(() =>
            {
                IMedia? root = _mediaService.GetRootMedia().FirstOrDefault(x =>
                    x.Name == ArticulateConstants.Convention.ArticulateMediaFolder &&
                    x.ContentType.Alias.InvariantEquals(Constants.Conventions.MediaTypes.Folder));
                return root ?? _mediaService.CreateMediaWithIdentity(
                    ArticulateConstants.Convention.ArticulateMediaFolder,
                    Constants.System.Root,
                    Constants.Conventions.MediaTypes.Folder);
            });
        }

        /// <inheritdoc/>
        public IMedia GetOrCreateArticulateMediaFolder() => _articulateMediaFolder.Value;

        /// <inheritdoc/>
        public ValueTask<ImportMediaValidationResult> ValidateImageAsync(Stream stream, string originalExtension)
        {
            ContentSettings contentSettings = _contentSettings.CurrentValue;

            var extension = originalExtension.ToLowerInvariant();
            if (!extension.StartsWith('.'))
            {
                extension = $".{extension}";
            }

            if (!contentSettings.IsFileAllowedForUpload(extension))
            {
                return ValueTask.FromResult(ImportMediaValidationResult.Failure(
                    $"Extension '{extension}' is blocked by Umbraco upload settings"));
            }

            FrozenSet<string> allowedExtensions = GetAllowedImageExtensions();
            if (!allowedExtensions.Contains(extension))
            {
                return ValueTask.FromResult(ImportMediaValidationResult.Failure(
                    $"Extension '{extension}' not allowed. Supported: {string.Join(", ", allowedExtensions)}"));
            }

            string? imageLimitError = TryGetMaxImportImageBytes(out long maxImportImageBytes);
            if (imageLimitError is not null)
            {
                return ValueTask.FromResult(ImportMediaValidationResult.Failure(imageLimitError));
            }
            if (stream.CanSeek && stream.Length > maxImportImageBytes)
            {
                return ValueTask.FromResult(ImportMediaValidationResult.Failure(
                    $"Image exceeded the configured limit of {maxImportImageBytes} bytes"));
            }

            return ValueTask.FromResult(ValidateImageSignature(stream, extension, allowedExtensions));
        }

        /// <inheritdoc/>
        public async Task<ImportMediaValidationResult> DecodeAndValidateBase64ImageAsync(
            string base64Content,
            string originalFileName)
        {
            if (string.IsNullOrWhiteSpace(base64Content))
            {
                return ImportMediaValidationResult.Failure("Base64 content is empty");
            }

            string? imageLimitError = TryGetMaxImportImageBytes(out long maxImportImageBytes);
            if (imageLimitError is not null)
            {
                return ImportMediaValidationResult.Failure(imageLimitError);
            }
            long? estimatedDecodedBytes = TryEstimateBase64DecodedBytes(base64Content);
            if (estimatedDecodedBytes is { } estimatedBytes && estimatedBytes > maxImportImageBytes)
            {
                return ImportMediaValidationResult.Failure(
                    $"Image exceeded the configured limit of {maxImportImageBytes} bytes");
            }

            byte[] bytes;
            try
            {
                bytes = Convert.FromBase64String(base64Content);
            }
            catch (FormatException)
            {
                return ImportMediaValidationResult.Failure("Invalid base64 content");
            }

            if (bytes.LongLength > maxImportImageBytes)
            {
                return ImportMediaValidationResult.Failure(
                    $"Image exceeded the configured limit of {maxImportImageBytes} bytes");
            }

            var stream = new MemoryStream(bytes);
            string extension = Path.GetExtension(originalFileName).ToLowerInvariant();

            ImportMediaValidationResult result = await ValidateImageAsync(stream, extension);
            if (!result.IsValid)
            {
                await stream.DisposeAsync();
            }

            return result;
        }

        private static long? TryEstimateBase64DecodedBytes(string base64Content)
        {
            long nonWhitespaceLength = CountNonWhitespaceCharacters(base64Content);

            if (nonWhitespaceLength == 0 || nonWhitespaceLength % 4 != 0)
            {
                return null;
            }

            return (nonWhitespaceLength / 4 * 3) - CountBase64PaddingCharacters(base64Content);
        }

        private static long CountNonWhitespaceCharacters(string value)
        {
            long count = 0;
            foreach (char c in value)
            {
                if (!char.IsWhiteSpace(c))
                {
                    count++;
                }
            }

            return count;
        }

        private static int CountBase64PaddingCharacters(string value)
        {
            var count = 0;
            for (var i = value.Length - 1; i >= 0 && count < 2; i--)
            {
                char c = value[i];
                if (char.IsWhiteSpace(c))
                {
                    continue;
                }

                if (c != '=')
                {
                    break;
                }

                count++;
            }

            return count;
        }

        /// <inheritdoc/>
        public async Task<ImportMediaValidationResult> DownloadAndValidateImageAsync(
            Uri imageUrl,
            CancellationToken cancellationToken = default)
        {
            try
            {
                using HttpClient templateClient = _httpClientFactory.CreateClient();
                using HttpClient client = CreatePinnedHttpClient(templateClient);
                (HttpResponseMessage response, Uri finalUri) = await SendWithValidatedRedirectsAsync(
                    client,
                    imageUrl,
                    cancellationToken);
                using (response)
                {
                    return await ProcessImageResponseAsync(response, finalUri, cancellationToken);
                }
            }
            catch (HttpRequestException ex)
            {
                return ImportMediaValidationResult.Failure($"Failed to download image: {ex.Message}");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return ImportMediaValidationResult.Failure("Image download was cancelled");
            }
            catch (TaskCanceledException)
            {
                return ImportMediaValidationResult.Failure("Image download timed out");
            }
        }

        /// <summary>
        /// Applies size-limit checks and image validation to an already-received HTTP response.
        /// Exposed as internal so unit tests can verify size-limiting without requiring a live HTTP server.
        /// </summary>
        internal async Task<ImportMediaValidationResult> ProcessImageResponseAsync(
            HttpResponseMessage response,
            Uri finalUri,
            CancellationToken cancellationToken = default)
        {
            if (!response.IsSuccessStatusCode)
            {
                return ImportMediaValidationResult.Failure($"HTTP error: {response.StatusCode}");
            }

            string? imageLimitError = TryGetMaxImportImageBytes(out long maxImportImageBytes);
            if (imageLimitError is not null)
            {
                return ImportMediaValidationResult.Failure(imageLimitError);
            }

            long? contentLength = response.Content.Headers.ContentLength;
            if (contentLength is { } knownLength && knownLength > maxImportImageBytes)
            {
                return ImportMediaValidationResult.Failure(
                    $"Image exceeded the configured limit of {maxImportImageBytes} bytes");
            }

            var memoryStream = new MemoryStream();
            await using Stream httpStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            bool copiedWithinLimit = await TryCopyToMemoryStreamAsync(
                httpStream,
                memoryStream,
                maxImportImageBytes,
                cancellationToken);

            if (!copiedWithinLimit)
            {
                await memoryStream.DisposeAsync();
                return ImportMediaValidationResult.Failure(
                    $"Image exceeded the configured limit of {maxImportImageBytes} bytes");
            }

            memoryStream.Position = 0;
            string extension = Path.GetExtension(finalUri.AbsolutePath).ToLowerInvariant();
            ImportMediaValidationResult result = await ValidateImageAsync(memoryStream, extension);

            if (!result.IsValid)
            {
                await memoryStream.DisposeAsync();
                return result;
            }

            memoryStream.Position = 0;
            return ImportMediaValidationResult.Success(
                memoryStream,
                result.CorrectExtension!,
                result.MimeType!);
        }

        private string? TryGetMaxImportImageBytes(out long maxImportImageBytes)
        {
            maxImportImageBytes = _articulateOptions.CurrentValue.MaxImportImageBytes;
            return maxImportImageBytes > 0
                ? null
                : "MaxImportImageBytes must be greater than zero";
        }

        /*
         * Implementation references:
         * - OWASP SSRF Prevention Cheat Sheet:
         *   https://cheatsheetseries.owasp.org/cheatsheets/Server_Side_Request_Forgery_Prevention_Cheat_Sheet.html
         * - .NET SocketsHttpHandler.ConnectCallback:
         *   https://learn.microsoft.com/en-us/dotnet/api/system.net.http.socketshttphandler.connectcallback
         * - HTTP redirection semantics (RFC 9110):
         *   https://www.rfc-editor.org/rfc/rfc9110.html#name-redirection-3xx
         * - IANA IPv4 / IPv6 special-purpose registries:
         *   https://www.iana.org/assignments/iana-ipv4-special-registry/iana-ipv4-special-registry.xhtml
         *   https://www.iana.org/assignments/iana-ipv6-special-registry/iana-ipv6-special-registry.xhtml
         * - Umbraco content settings:
         *   https://docs.umbraco.com/umbraco-cms/reference/configuration/contentsettings
         *   https://docs.umbraco.com/umbraco-cms/reference/configuration/imagingsettings
         */
        private async Task<(HttpResponseMessage Response, Uri FinalUri)> SendWithValidatedRedirectsAsync(
            HttpClient client,
            Uri imageUrl,
            CancellationToken cancellationToken)
        {
            // OWASP SSRF guidance recommends validating each redirect target, not just the initial URL.
            // We handle redirects manually so every hop is re-checked against the Umbraco host allowlist,
            // IP safety rules, and pinned-connection transport.
            Uri currentUri = imageUrl;

            for (var redirectCount = 0; redirectCount <= MaxRedirects; redirectCount++)
            {
                IPAddress[] pinnedAddresses = await GetValidatedPinnedAddressesAsync(currentUri, cancellationToken);

                HttpResponseMessage response = await SendPinnedRequestAsync(
                    client,
                    currentUri,
                    pinnedAddresses,
                    cancellationToken);

                if (!IsRedirectStatusCode(response.StatusCode))
                {
                    return (response, currentUri);
                }

                Uri redirectUri = GetValidatedRedirectUri(response, currentUri, redirectCount);
                response.Dispose();
                currentUri = redirectUri;
            }

            throw new HttpRequestException("Image redirect handling failed unexpectedly");
        }

        private async Task<IPAddress[]> GetValidatedPinnedAddressesAsync(Uri imageUrl, CancellationToken cancellationToken)
        {
            (IPAddress[]? pinnedAddresses, string? validationError) =
                await ValidateExternalImageUrlAsync(imageUrl, cancellationToken);

            if (validationError is not null || pinnedAddresses is null || pinnedAddresses.Length == 0)
            {
                throw new HttpRequestException(validationError ?? "Image URL rejected");
            }

            return pinnedAddresses;
        }

        /// <summary>
        /// Validates and resolves a redirect response without requiring a live HTTP server.
        /// </summary>
        internal static Uri GetValidatedRedirectUri(HttpResponseMessage response, Uri currentUri, int redirectCount)
        {
            if (redirectCount == MaxRedirects)
            {
                response.Dispose();
                throw new HttpRequestException("Too many redirects while downloading image");
            }

            if (response.Headers.Location is null)
            {
                response.Dispose();
                throw new HttpRequestException("Redirect response did not contain a Location header");
            }

            Uri redirectUri = response.Headers.Location.IsAbsoluteUri
                ? response.Headers.Location
                : new Uri(currentUri, response.Headers.Location);

            if (currentUri.Scheme == Uri.UriSchemeHttps && redirectUri.Scheme != Uri.UriSchemeHttps)
            {
                response.Dispose();
                throw new HttpRequestException("Image redirects cannot downgrade from HTTPS");
            }

            return redirectUri;
        }

        // Redirect status codes follow HTTP Semantics (RFC 9110 section 15.4), plus 308 Permanent Redirect.
        private static bool IsRedirectStatusCode(HttpStatusCode statusCode) =>
            statusCode == HttpStatusCode.Moved ||
            statusCode == HttpStatusCode.Redirect ||
            statusCode == HttpStatusCode.RedirectMethod ||
            (int)statusCode == 307 ||
            (int)statusCode == 308;

        private async Task<bool> TryCopyToMemoryStreamAsync(
            Stream source,
            MemoryStream destination,
            long maxBytes,
            CancellationToken cancellationToken)
        {
            byte[] buffer = new byte[81920];
            long totalBytes = 0;

            while (true)
            {
                int bytesRead = await source.ReadAsync(buffer, cancellationToken);
                if (bytesRead == 0)
                {
                    break;
                }

                totalBytes += bytesRead;
                if (totalBytes > maxBytes)
                {
                    return false;
                }

                await destination.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
            }

            return true;
        }

        private FrozenSet<string> GetAllowedImageExtensions()
        {
            string[] configuredExtensions = _contentSettings.CurrentValue.Imaging.ImageFileTypes
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.StartsWith('.') ? x.ToLowerInvariant() : $".{x.ToLowerInvariant()}")
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            return configuredExtensions.Length == 0
                ? _fallbackAllowedExtensions
                : configuredExtensions.ToFrozenSet();
        }

        private ImportMediaValidationResult ValidateImageSignature(
            Stream stream,
            string requestedExtension,
            FrozenSet<string> allowedExtensions)
        {
            if (!stream.CanSeek)
            {
                return ImportMediaValidationResult.Failure("Image stream must be seekable");
            }

            stream.Position = 0;
            FileFormat? detectedFormat = _fileFormatInspector.DetermineFileFormat(stream);
            stream.Position = 0;

            if (detectedFormat is not Image imageFormat)
            {
                return ImportMediaValidationResult.Failure(
                    "File does not appear to be a valid image");
            }

            string detectedExtension = NormalizeExtension($".{imageFormat.Extension}");
            string canonicalExtension = GetCanonicalImageExtension(requestedExtension, detectedExtension, allowedExtensions);
            if (!allowedExtensions.Contains(canonicalExtension))
            {
                return ImportMediaValidationResult.Failure(
                    $"Detected image format '{detectedExtension}' not allowed. Supported: {string.Join(", ", allowedExtensions)}");
            }

            string mimeType = canonicalExtension.GetImageMimeType();
            return ImportMediaValidationResult.Success(stream, canonicalExtension, mimeType);
        }

        private async Task<(IPAddress[]? PinnedAddresses, string? Error)> ValidateExternalImageUrlAsync(
            Uri imageUrl,
            CancellationToken cancellationToken)
        {
            // Articulate provides the explicit host allowlist via Articulate:AllowedMediaHosts.
            // After host validation we still resolve and vet the destination IPs per OWASP SSRF guidance.
            Options.ArticulateOptions articulateOptions = _articulateOptions.CurrentValue;
            bool isProductionMode = _runtimeSettings.CurrentValue.Mode == RuntimeMode.Production;
            bool allowUnsafeLocalExternalImageHosts =
                !isProductionMode &&
                articulateOptions.AllowUnsafeLocalExternalImageHostsInDevelopment;

            string? validationError = ValidateExternalImageUri(imageUrl);
            if (validationError is not null)
            {
                return (null, validationError);
            }

            ISet<string> allowedHosts = articulateOptions.AllowedMediaHosts.ToHashSet(StringComparer.OrdinalIgnoreCase);
            validationError = ExternalImageHostPolicy.ValidateHost(
                imageUrl.Host,
                allowedHosts,
                allowUnsafeLocalExternalImageHosts,
                isProductionMode);
            if (validationError is not null)
            {
                return (null, validationError);
            }

            IPAddress[] addresses;
            try
            {
                addresses = await Dns.GetHostAddressesAsync(imageUrl.DnsSafeHost, cancellationToken);
            }
            catch (SocketException ex)
            {
                return (null, $"Could not resolve image host '{imageUrl.Host}': {ex.Message}");
            }

            if (addresses.Length == 0)
            {
                return (null, $"Could not resolve any addresses for image host '{imageUrl.Host}'");
            }

            validationError = ExternalImageHostPolicy.ValidateResolvedAddresses(
                addresses,
                imageUrl.Host,
                allowUnsafeLocalExternalImageHosts);
            if (validationError is not null)
            {
                return (null, validationError);
            }

            return (addresses, null);
        }

        private static string? ValidateExternalImageUri(Uri imageUrl) =>
            !imageUrl.IsAbsoluteUri ||
            (imageUrl.Scheme != Uri.UriSchemeHttp && imageUrl.Scheme != Uri.UriSchemeHttps)
                ? "Only absolute HTTP(S) image URLs are allowed"
                : null;

        private async Task<HttpResponseMessage> SendPinnedRequestAsync(
            HttpClient client,
            Uri requestUri,
            IReadOnlyList<IPAddress> pinnedAddresses,
            CancellationToken cancellationToken)
        {
            Exception? lastException = null;

            foreach (IPAddress pinnedAddress in pinnedAddresses)
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
                request.Options.Set(_pinnedAddressOption, pinnedAddress);

                try
                {
                    return await client.SendAsync(
                        request,
                        HttpCompletionOption.ResponseHeadersRead,
                        cancellationToken);
                }
                catch (Exception ex) when (ex is HttpRequestException or SocketException)
                {
                    lastException = ex;
                }
            }

            throw new HttpRequestException(
                $"Failed to connect to any resolved address for '{requestUri.Host}'",
                lastException);
        }

        internal static SocketsHttpHandler CreatePinnedHttpHandler() =>
            new()
            {
                AllowAutoRedirect = false,
                AutomaticDecompression = DecompressionMethods.All,
                UseProxy = false,
                ConnectCallback = async (context, cancellationToken) =>
                {
                    if (!context.InitialRequestMessage.Options.TryGetValue(_pinnedAddressOption, out IPAddress? pinnedAddress))
                    {
                        throw new HttpRequestException("No validated IP address was available for the outbound image request");
                    }

                    var socket = new Socket(pinnedAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

                    try
                    {
                        await socket.ConnectAsync(pinnedAddress, context.DnsEndPoint.Port, cancellationToken);
                        return new NetworkStream(socket, ownsSocket: true);
                    }
                    catch
                    {
                        socket.Dispose();
                        throw;
                    }
                }
            };

        internal static HttpClient CreatePinnedHttpClient(HttpClient templateClient)
        {
            SocketsHttpHandler handler = CreatePinnedHttpHandler();

            HttpClient client = new(handler, disposeHandler: true)
            {
                BaseAddress = templateClient.BaseAddress,
                DefaultRequestVersion = templateClient.DefaultRequestVersion,
                DefaultVersionPolicy = templateClient.DefaultVersionPolicy,
                MaxResponseContentBufferSize = templateClient.MaxResponseContentBufferSize,
                Timeout = templateClient.Timeout
            };

            // Do not copy any default headers from the template client. Even non-credential
            // headers can reveal local deployment details or be confused for trusted auth context.
            return client;
        }

        private static string NormalizeExtension(string extension)
        {
            string normalized = extension.ToLowerInvariant();
            return normalized.StartsWith('.') ? normalized : $".{normalized}";
        }

        private static string GetCanonicalImageExtension(
            string requestedExtension,
            string detectedExtension,
            FrozenSet<string> allowedExtensions)
        {
            if (IsEquivalentJpegExtension(requestedExtension, detectedExtension) &&
                allowedExtensions.Contains(requestedExtension))
            {
                return requestedExtension;
            }

            return detectedExtension;
        }

        private static bool IsEquivalentJpegExtension(string requestedExtension, string detectedExtension) =>
            (requestedExtension, detectedExtension) is
            (".jpg", ".jpeg") or
            (".jpeg", ".jpg");

        /// <inheritdoc/>
        public ImportMediaSaveResult SaveToMediaLibrary(
            Stream imageStream,
            string mediaName,
            string extension,
            IMedia? parentFolder = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(mediaName))
                {
                    throw new ArgumentException(@"Media name cannot be empty", nameof(mediaName));
                }

                // Strip extension to avoid doubling up (e.g., image.png -> image-png.png)
                var cleanMediaName = Path.GetFileNameWithoutExtension(mediaName);
                if (string.IsNullOrWhiteSpace(cleanMediaName))
                {
                    cleanMediaName = "image";
                }

                var safeFileName = $"{cleanMediaName.ToSafeFileName(_shortStringHelper)}{extension}";

                // Display name for backoffice - ToFriendlyName strips extensions and applies Title Case
                var displayName = safeFileName.ToFriendlyName();
                if (string.IsNullOrWhiteSpace(displayName))
                {
                    displayName = "Image";
                }

                if (displayName.Length > 100)
                {
                    displayName = displayName[..100];
                }

                var parentId = parentFolder?.Id ?? Constants.System.Root;

                IMedia media = _mediaService.CreateMedia(displayName, parentId, Constants.Conventions.MediaTypes.Image);
                media.SetValue(
                    _mediaFileManager,
                    _mediaUrlGenerators,
                    _shortStringHelper,
                    _contentTypeBaseServiceProvider,
                    Constants.Conventions.Media.File,
                    safeFileName,
                    imageStream);

                Attempt<OperationResult?> saveResult = _mediaService.Save(media);
                if (!saveResult.Success)
                {
                    return ImportMediaSaveResult.Failed($"Failed to save media item: {displayName}");
                }

                var udi = Udi.Create(Constants.UdiEntityType.Media, media.Key).ToString();
                return ImportMediaSaveResult.Succeeded(media, udi);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving image to media library");
                return ImportMediaSaveResult.Failed($"Unexpected error: {ex.Message}");
            }
        }

        /// <inheritdoc/>
        public string SaveToFileSystem(Stream imageStream, string extension, string? originalFileName = null)
        {
            try
            {
                // 8-char GUID folder for file isolation
                var uniqueFolder = Guid.NewGuid().ToString("N")[..8];
                string safeFileName;

                if (!string.IsNullOrWhiteSpace(originalFileName))
                {
                    var fileNameWithoutExt = Path.GetFileNameWithoutExtension(originalFileName);
                    var sanitized = fileNameWithoutExt.ToSafeFileName(_shortStringHelper);
                    safeFileName = $"{sanitized}{extension}";
                }
                else
                {
                    safeFileName = $"{Guid.NewGuid():N}{extension}".ToSafeFileName(_shortStringHelper);
                }

                var fileUrl = $"articulate/{uniqueFolder}/{safeFileName}";

                imageStream.Position = 0;
                _mediaFileManager.FileSystem.AddFile(fileUrl, imageStream);

                var fileSystemUrl = _mediaFileManager.FileSystem.GetUrl(fileUrl);
                return _absoluteUrlBuilder.ToAbsoluteUrl(fileSystemUrl).ToString();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving image to file system");
                return string.Empty;
            }
        }
    }
}
