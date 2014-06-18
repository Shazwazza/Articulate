using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;

namespace Articulate.Models
{
    [DataContract]
    public class ImportBlogMlModel
    {
        [DataMember(Name = "articulateNode", IsRequired = true)]
        [Required]
        public int ArticulateNodeId { get; set; }

        [DataMember(Name = "overwrite")]
        public bool Overwrite { get; set; }

        [DataMember(Name = "regexMatch")]
        public string RegexMatch { get; set; }

        [DataMember(Name = "regexReplace")]
        public string RegexReplace { get; set; }

        [DataMember(Name = "publish")]
        public bool Publish { get; set; }

        [DataMember(Name = "tempFile", IsRequired = true)]
        [Required]
        public string TempFile { get; set; }

        [DataMember(Name = "exportDisqusXml")]
        public bool ExportDisqusXml { get; set; }
    }
}