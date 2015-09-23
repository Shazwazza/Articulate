using CookComputing.XmlRpc;

namespace Articulate.Models.MetaWeblog
{
    public interface IWordPressMetaWeblog
    {
        [XmlRpcMethod("wp.getTags")]
        object[] GetTags(string blogid, string username, string password);
    }
}