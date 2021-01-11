//-----------------------------------------------------------------------------
// FILE:	    KubeNodePrepare.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2021 by neonFORGE LLC.  All rights reserved.
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
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Neon.Common;
using Neon.IO;
using Neon.SSH;

using Renci.SshNet;

namespace Neon.Kube
{
    /// <summary>
    /// Handles preparing nodes for inclusion in a cluster.
    /// </summary>
    public static class KubeNodePrepare
    {
        /// <summary>
        /// Static constructor.
        /// </summary>
        static KubeNodePrepare()
        {
            Resources = Assembly.GetExecutingAssembly().GetResourceFileSystem("Neon.Kube.Resources");
        }

        /// <summary>
        /// Returns a <see cref="IStaticDirectory"/> holding this assembly's resources.
        /// </summary>
        public static IStaticDirectory Resources { get; private set; }

        /// <summary>
        /// Customizes the OpenSSH configuration on a node.
        /// </summary>
        /// <param name="node">The target node.</param>
        /// <param name="stepDelayed">Ignored.</param>
        public static void ConfigureOpenSsh(NodeSshProxy<NodeDefinition> node, TimeSpan stepDelayed)
        {
            // Upload the OpenSSH server configuration and restart OpenSSH.

            node.UploadText("/etc/ssh/sshd_config", KubeHelper.OpenSshConfig);
            node.SudoCommand("systemctl restart sshd");
        }
    }
}
