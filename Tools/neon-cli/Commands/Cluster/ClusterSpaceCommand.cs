//-----------------------------------------------------------------------------
// FILE:	    ClusterSpaceCommand.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

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

using Neon.Common;
using Neon.Kube;

namespace NeonCli
{
    /// <summary>
    /// Implements the <b>cluster space</b> command.
    /// </summary>
    [Command]
    public class ClusterSpaceCommand : CommandBase
    {
        private const string usage = @"
Configures [neon-cli] to manage clusters in separate clusterspaces created 
by automation like [ClusterFixture] for unit testing.  The logins and other
cluster related information can be found under:

    ~/.neonkube/spaces/SPACE-NAME

[ClusterFixture] manages the clusters it deploys under this directory:

    ~/.neonkube/spaces/$fixture

USAGE:

    Prints the current cluster space:

        neon cluster space

    Changes the clusternamespace:

        neon cluster space SPACE-NAME

    Resets the clusterspace to the default:

        neon cluster space --reset

REMARKS:

    Standard Kubernetes tools like [kubectl] and [helm] perform operations on
    a [current] kubeconfig context holding the information and credentials
    required to access the cluster.  Information about known clusters is
    generally stored in the user's [~/.kube/config] file with a field in that
    file identifying the [current] cluster.  The [neon-cli] tool also follows
    these conventions.

    neonKUBE requires additional information about clusters and by default 
    persists this information at [~/.neonkube/logins/*].

    The problem with this model is that only one cluster can be current at
    any given time and it's often useful to perform operations on multiple
    cluster simultaneously (i.e. running a built-in neonDESKTOP cluster while
    also running unit tests against a test cluster on the same machine).

    We're introducing the concept of [clusterspaces] for this.  A cluster
    space holds the information about clusters and allows on of them to
    be selected as [current].  The normal Kubernetes [~/.kube/config file 
    along with the additional neonKUBE information is known as the [default]
    space.

    Clusterspaces are managed in at [~/.neonkube/spaces/SPACE-NAME/*] where 
    SPACE-NAME identifies the clusterspace.  A separate [.kube/config] file
    will be created here with the Kubernetes contexts for the clusters in the 
    space and other folders will be present with the neonKUBE related cluster
    information.  [neon-cli] modifies the KUBECONFIG environment variable
    along with the [~/.neonkube/current-space] file to manage the current 
    space for subsequent [neon-cli] commands.

    [ClusterFixture] currently creates the ""$fixture"" clusterspace where
    cluster information about test clusters is managed.    

    NOTE:

    Clusterspaces are currently limited to providing a way to use [neon-cli]
    against clusters deployed by [ClusterFixture].  In the future, this feature
    may be extended to support user managed clusterspaces.

    neonDESKTOP is not impacted by clusterspaces and currently only manages
    clusters in the default clusterspace.
";

        /// <inheritdoc/>
        public override string[] Words => new string[] { "cluster", "space" };

        /// <inheritdoc/>
        public override string[] ExtendedOptions => new string[] { "--reset" };

        /// <inheritdoc/>
        public override void Help()
        {
            Console.WriteLine(usage);
        }

        /// <inheritdoc/>
        public override async Task RunAsync(CommandLine commandLine)
        {
            Console.WriteLine();

            var clusterspace = commandLine.Arguments.ElementAtOrDefault(0);

            if (clusterspace == null)
            {
                if (commandLine.HasOption("--reset"))
                {
                    KubeHelper.CurrentClusterSpace = null;

                    Console.WriteLine("Current clusterspace is now: default");
                    Program.Exit(0);
                }

                Console.WriteLine($"Current clusterspace: {KubeHelper.CurrentClusterSpace ?? "default"}");
                Program.Exit(0);
            }

            KubeHelper.CurrentClusterSpace = clusterspace;

            Console.WriteLine($"Cluster clusterspace is now: {clusterspace}");
            await Task.CompletedTask;
        }
    }
}
