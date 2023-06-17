using ProtoBuf;
using System.Collections.Generic;

namespace ProspectTogether.Shared
{
    [ProtoContract(ImplicitFields = ImplicitFields.None)]
    public class ProspectingPacket
    {
        [ProtoMember(1)]
        public readonly List<ProspectInfo> Data;

        [ProtoMember(2)]
        public readonly bool OriginatesFromProPick;

        ProspectingPacket()
        {
            Data = new List<ProspectInfo>();
        }

        public ProspectingPacket(List<ProspectInfo> data, bool originatesFromProPick)
        {
            Data = data;
            OriginatesFromProPick = originatesFromProPick;
        }
    }
}
