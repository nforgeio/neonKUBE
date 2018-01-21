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

    neon cluster add        LOGIN-PATH
    neon cluster example
    neon cluster get        USER@CLUSTER
    neon cluster list
    neon cluster ls
    neon cluster prepare    CLUSTER-DEF
    neon cluster property   VALUE
    neon cluster property   NODE.VALUE
    neon cluster remove     USER@CLUSTER
    neon cluster rm         USER@CLUSTER
    neon cluster setup      CLUSTER-DEF
    neon cluster verify     CLUSTER-DEF

ARGUMENTS:

    CLUSTER-DEF         - Path to a cluster definition file.  This is
                          optional for some commands when logged in.
    LOGIN-PATH          - Path to a cluster login file including
                          the cluster definition and user credentials.
    NODE                - Optionally identifies a specific node.
    PATH                - File path.
    USER@CLUSTER        - Specifies a cluster login username and cluster.
    VALUE               - Identifies the desired value
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
        public override ShimInfo Shim(DockerShim shim)
        {
            return new ShimInfo(isShimmed: true);
        }
    }
}
