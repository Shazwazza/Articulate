using System;
using System.IO;
using Umbraco.Core.IO;

namespace Articulate.Controllers
{
    internal static class FileSystemExtensions
    {
        //TODO: Currently this is the only way to do this
        internal static void CreateFolder(this IFileSystem fs, string folderPath)
        {
            var path = fs.GetRelativePath(folderPath);
            var tempFile = Path.Combine(path, Guid.NewGuid().ToString("N") + ".tmp");
            using (var s = new MemoryStream())
            {
                fs.AddFile(tempFile, s);
            }
            fs.DeleteFile(tempFile);
        }
    }
}