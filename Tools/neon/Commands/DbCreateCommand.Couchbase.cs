//-----------------------------------------------------------------------------
// FILE:	    DbCreateCommand.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2017 by NeonForge, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft;
using Newtonsoft.Json;

using Neon.Cluster;
using Neon.Common;

namespace NeonTool
{
    public partial class DbCreateCommand : CommandBase
    {
        /// <summary>
        /// Deploy Couchbase.
        /// </summary>
        private void CreateCouchbase()
        {
            var image = commandLine.GetOption("--image", "NeonCluster/Couchbase:latest");

        }
    }
}
