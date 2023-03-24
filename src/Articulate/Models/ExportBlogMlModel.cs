using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;

namespace Articulate.Models
{
    [DataContract]
    public class ExportBlogMlModel
    {
        [DataMember(Name = "articulateNode", IsRequired = true)]
        [Required]
        public int ArticulateNodeId { get; set; }

        [DataMember(Name = "exportImagesAsBase64")]
        public bool ExportImagesAsBase64 { get; set; } = false;
    }
}
