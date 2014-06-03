using System.Collections.Generic;
using CookComputing.XmlRpc;

namespace Articulate.Models.MetaWeblog
{
    [XmlRpcMissingMapping(MappingAction.Ignore)]
    public struct MediaObject
    {
        public string name;
        public string type;
        public byte[] bits;
    }
}