#nullable enable
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Hosting;
using Umbraco.Cms.Core.IO;

namespace Articulate.ImportExport
{
    /// <summary>
    ///     A temporary file system for Articulate import/export operations.
    /// </summary>
    public class ArticulateTempFileSystem(
        IIOHelper ioHelper,
        IHostingEnvironment hostingEnvironment,
        ILogger<ArticulateTempFileSystem> logger)
        : PhysicalFileSystem(ioHelper, hostingEnvironment, logger, ArticulateConstants.Paths.ArticulateTemp,
            Guid.NewGuid().ToString());
}
