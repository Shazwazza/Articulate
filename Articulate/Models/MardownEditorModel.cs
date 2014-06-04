using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;

namespace Articulate.Models
{
    [DataContract]
    public class MardownEditorModel
    {
        [Required]
        [DataMember(Name = "articulateNodeId", IsRequired = true)]
        public int? ArticulateNodeId { get; set; }

        [Required]
        [DataMember(Name = "title", IsRequired = true)]
        public string Title { get; set; }

        [Required]
        [DataMember(Name = "body", IsRequired = true)]
        public string Body { get; set; }

        [DataMember(Name = "tags")]
        public string Tags { get; set; }

        [DataMember(Name = "categories")]
        public string Categories { get; set; }

        [DataMember(Name = "slug")]
        public string Slug { get; set; }

        [DataMember(Name = "excerpt")]
        public string Excerpt { get; set; }
    }
}