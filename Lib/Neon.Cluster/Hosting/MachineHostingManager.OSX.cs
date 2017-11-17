//-----------------------------------------------------------------------------
// FILE:	    MachineHostingManager.OSX.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2017 by NeonForge, LLC.  All rights reserved.

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
        /// <param name = "force" > Specifies whether any existing named VMs are to be stopped and overwritten.</param>
        private void DeployOsxVMs(bool force)
        {
            // $todo(jeff.lill): Implement this.

            throw new NotImplementedException();
        }
    }
}
