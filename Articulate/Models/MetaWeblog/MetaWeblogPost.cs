using System;
using System.Web;
using CookComputing.XmlRpc;

namespace Articulate.Models.MetaWeblog
{
    [XmlRpcMissingMapping(MappingAction.Ignore)]
    public class MetaWeblogPost
    {
        public MetaWeblogPost()
        {
            Id = Guid.Empty.ToString();
            Title = "";
            Author = "";
            Content = "";
            CreateDate = DateTime.MinValue;
            Categories = new string[0];
            Tags = "";
        }

        [XmlRpcMember("postid")]
        public string Id { get; set; }

        [XmlRpcMember("title")]
        public string Title { get; set; }

        [XmlRpcMember("mt_excerpt")]
        public string Excerpt { get; set; }

        [XmlRpcMember("author")]
        public string Author { get; set; }

        [XmlRpcMember("wp_slug")]
        public string Slug { get; set; }

        [XmlRpcMember("description")]
        public string Content { get; set; }

        [XmlRpcMember("dateCreated")]
        public DateTime CreateDate { get; set; }
        
        [XmlRpcMember("categories")]
        public string[] Categories { get; set; }

        [XmlRpcMember("mt_keywords")]
        public string Tags { get; set; }

        [XmlRpcMember("mt_allow_comments")]
        public int AllowComments;

    }
}