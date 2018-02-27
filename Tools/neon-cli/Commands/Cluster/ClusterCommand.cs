//-----------------------------------------------------------------------------
// FILE:	    ClusterCommand.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

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

namespace NeonCli
{
    /// <summary>
    /// Implements the <b>cluster</b> command.
    /// </summary>
    public class ClusterCommand : CommandBase
    {
        private const string usage = @"
Performs basic cluster provisioning and management.

USAGE:

    neon cluster example
    neon cluster get        USER@CLUSTER
    neon cluster prepare    CLUSTER-DEF
    neon cluster setup      CLUSTER-DEF
    neon cluster verify     CLUSTER-DEF

ARGUMENTS:

    CLUSTER-DEF         - Path to a cluster definition file.  This is
                          optional for some commands when logged in.
    USER@CLUSTER        - Specifies a cluster login username and cluster.
";

        /// <inheritdoc/>
        public override string[] Words
        {
            get { return new string[] { "cluster" }; }
        }

        /// <inheritdoc/>
        public override void Help()
        {
            Console.WriteLine(usage);
        }

        /// <inheritdoc/>
        public override void Run(CommandLine commandLine)
        {
            Help();
        }

        /// <inheritdoc/>
        public override DockerShimInfo Shim(DockerShim shim)
        {
            return new DockerShimInfo(isShimmed: true);
        }
    }
}
