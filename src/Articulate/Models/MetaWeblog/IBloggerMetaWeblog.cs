using CookComputing.XmlRpc;

namespace Articulate.Models.MetaWeblog
{
    public interface IBloggerMetaWeblog
    {
        [XmlRpcMethod("blogger.deletePost")]
        [return: XmlRpcReturnValue(Description = "Returns true.")]
        bool DeletePost(string key, string postid, string username, string password, bool publish);

        [XmlRpcMethod("blogger.getUsersBlogs")]
        object[] GetUsersBlogs(string key, string username, string password);
    }
}