using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CSharpServerFramework.Extension;
using CSharpServerFramework;
using CSServerJsonProtocol;

namespace Chicago.Extension
{
    [ExtensionInfo("HeartBeat")]
    public class HeartBeatExtension : JsonExtensionBase
    {
        public override void Init()
        {
        }

        [CommandInfo("Beat")]
        public void ClientBeat(ICSharpServerSession session, dynamic msg)
        {
            this.SendJsonResponse(session, new { smile = ":)" }, ExtensionName, "Beat");
        }
    }
}
