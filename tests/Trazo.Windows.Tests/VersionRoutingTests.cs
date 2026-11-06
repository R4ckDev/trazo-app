using System;
using System.Threading;

namespace Trazo
{
    internal static class VersionRoutingTests
    {
        internal static int Run()
        {
            string current="qa-current-"+Guid.NewGuid().ToString("N"),older="qa-older-"+Guid.NewGuid().ToString("N");
            if(Program.IsVersionRunning(current))throw new Exception("No instance exists before the version marker is created");
            using(EventWaitHandle marker=new EventWaitHandle(false,EventResetMode.ManualReset,Program.VersionMarkerName(older)))
            {
                if(!Program.IsVersionRunning(older))throw new Exception("Running version is identified by its own live marker");
                if(Program.IsVersionRunning(current))throw new Exception("A different running version cannot silently receive the new version's requests");
            }
            if(Program.IsVersionRunning(older))throw new Exception("Closing a version releases its running-instance marker");
            return 4;
        }
    }
}
