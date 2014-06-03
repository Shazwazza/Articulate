using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CookComputing.XmlRpc;

namespace Articulate.Models.MetaWeblog
{
    //http://codex.wordpress.org/XML-RPC_MetaWeblog_API
    //http://xmlrpc.scripting.com/metaWeblogApi.html
    public interface IMetaWeblog
    {
        [XmlRpcMethod("metaWeblog.newPost")]
        string AddPost(string blogid, string username, string password, MetaWeblogPost post, bool publish);

        [XmlRpcMethod("metaWeblog.editPost")]
        bool UpdatePost(string postid, string username, string password, MetaWeblogPost post, bool publish);

        [XmlRpcMethod("metaWeblog.getPost")]
        object GetPost(string postid, string username, string password);

        [XmlRpcMethod("metaWeblog.getCategories")]
        object[] GetCategories(string blogid, string username, string password);

        [XmlRpcMethod("metaWeblog.getRecentPosts")]
        object[] GetRecentPosts(string blogid, string username, string password, int numberOfPosts);

        [XmlRpcMethod("metaWeblog.newMediaObject")]
        object NewMediaObject(string blogid, string username, string password, MetaWeblogMediaObject mediaObject);

        [XmlRpcMethod("metaWeblog.getUsersBlogs")]
        object[] GetUsersBlogs(string key, string username, string password);
    }

    //NOTE: We are not implementing all of this, we are not supporting pages or other blogger specifics

    //http://codex.wordpress.org/XML-RPC_wp
    //http://codex.wordpress.org/XML-RPC_MetaWeblog_API
    //NOTE: We are not implementing all of this, we are not supporting pages or other wp specifics
}
