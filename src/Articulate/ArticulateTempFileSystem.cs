using Umbraco.Core.IO;

namespace Articulate
{
    public class ArticulateTempFileSystem : PhysicalFileSystem
    {
        public ArticulateTempFileSystem(string virtualRoot) : base(virtualRoot)
        {
        }
    }
}