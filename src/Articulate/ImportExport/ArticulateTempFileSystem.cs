using System;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Hosting;
using Umbraco.Cms.Core.IO;

namespace Articulate.ImportExport
{
    public class ArticulateTempFileSystem : PhysicalFileSystem
    {
        public ArticulateTempFileSystem(
            IIOHelper ioHelper,
            IHostingEnvironment hostingEnvironment,
            ILogger<PhysicalFileSystem> logger)
            : base(ioHelper, hostingEnvironment, logger, "Articulate/Temp", Guid.NewGuid().ToString())
        {
        }
    }
}
