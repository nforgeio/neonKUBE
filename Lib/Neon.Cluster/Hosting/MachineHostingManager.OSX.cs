//-----------------------------------------------------------------------------
// FILE:	    MachineHostingManager.OSX.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2017 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Neon.Common;
using Neon.Cryptography;
using Neon.IO;
using Neon.Net;
using Neon.Time;

namespace Neon.Cluster
{
    public partial class MachineHostingManager : HostingManager
    {
        /// <summary>
        /// Handles the deploymenmt of the cluster virtual machines on 
        /// Macintosh OSX.
        /// </summary>
        private void PrepareVirtualBox()
        {
            // $todo(jeff.lill): Implement this.

            throw new NotImplementedException();
        }

        /// <summary>
        /// Perform any necessary global post VirtualBox provisioning steps.
        /// </summary>
        private void FinishVirtualBox()
        {
            // Recreate the node proxies because we disposed them above.
            // We need to do this so subsequent prepare steps will be
            // able to connect to the nodes via the correct addresses.

            cluster.CreateNodes();
        }

        /// <summary>
        /// Creates node virtual machine in Hyper-V.
        /// </summary>
        /// <param name="node">The target node.</param>
        private void ProvisionVirtualBoxMachine(NodeProxy<NodeDefinition> node)
        {
        }
    }
}
