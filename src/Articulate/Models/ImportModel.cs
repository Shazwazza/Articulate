using System.Runtime.Serialization;

namespace Articulate.Models
{
    [DataContract]
    public class ImportModel
    {
        [DataMember(Name = "downloadUrl")]
        public string DownloadUrl { get; set; }
    }
}