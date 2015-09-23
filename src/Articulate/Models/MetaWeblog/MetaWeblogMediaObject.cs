using System.Collections.Generic;
using CookComputing.XmlRpc;

namespace Articulate.Models.MetaWeblog
{
    [XmlRpcMissingMapping(MappingAction.Ignore)]
    public struct MetaWeblogMediaObject
    {
        [XmlRpcMember("name")]
        public string Name;

        [XmlRpcMember("type")]
        public string Type;

        [XmlRpcMember("bits")]
        public byte[] Bits;
    }
}