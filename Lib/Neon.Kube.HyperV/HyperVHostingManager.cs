//-----------------------------------------------------------------------------
// FILE:	    HyperVHostingManager.cs
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

// $todo(jefflill): Remove this after we actually implement this hosting manager.

#pragma warning disable CS0169

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

using Neon.Collections;
using Neon.Common;
using Neon.Cryptography;
using Neon.IO;
using Neon.Net;
using Neon.SSH;
using Neon.Time;

namespace Neon.Kube
{
    /// <summary>
    /// Manages cluster provisioning on remote Hyper-V servers.
    /// </summary>
    [HostingProvider(HostingEnvironment.HyperV)]
    public class HyperVHostingManager : HostingManager
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Ensures that the assembly hosting this hosting manager is loaded.
        /// </summary>
        public static void Load()
        {
            // We don't have to do anything here because the assembly is loaded
            // as a byproduct of calling this method.
        }

        //---------------------------------------------------------------------
        // Instance members

        private ClusterProxy                    cluster;
        private SetupController<NodeDefinition> controller;
        private string                          driveTemplatePath;
        private string                          vmDriveFolder;
        private string                          switchName;

        /// <summary>
        /// Creates an instance that is only capable of validating the hosting
        /// related options in the cluster definition.
        /// </summary>
        public HyperVHostingManager()
        {
        }

        /// <summary>
        /// Creates an instance that is capable of provisioning a cluster on Hyper-V servers.
        /// </summary>
        /// <param name="cluster">The cluster being managed.</param>
        /// <param name="logFolder">
        /// The folder where log files are to be written, otherwise or <c>null</c> or 
        /// empty if logging is disabled.
        /// </param>
        public HyperVHostingManager(ClusterProxy cluster, string logFolder = null)
        {
            Covenant.Requires<ArgumentNullException>(cluster != null, nameof(cluster));

            cluster.HostingManager = this;

            this.cluster = cluster;
        }

        /// <inheritdoc/>
        public override void Dispose(bool disposing)
        {
            if (disposing)
            {
                GC.SuppressFinalize(this);
            }
        }

        /// <inheritdoc/>
        public override HostingEnvironment HostingEnvironment => HostingEnvironment.HyperV;

        /// <inheritdoc/>
        public override bool RequiresNodeAddressCheck => true;

        /// <inheritdoc/>
        public override void Validate(ClusterDefinition clusterDefinition)
        {
            Covenant.Requires<ArgumentNullException>(clusterDefinition != null, nameof(clusterDefinition));
        }

        /// <inheritdoc/>
        public override bool RequiresAdminPrivileges => false;

        /// <inheritdoc/>
        public override void AddProvisioningSteps(SetupController<NodeDefinition> controller)
        {
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Assert(cluster != null, $"[{nameof(HyperVHostingManager)}] was created with the wrong constructor.");

            this.controller = controller;

            throw new NotImplementedException("$todo(jefflill): Implement this.");
        }

        /// <inheritdoc/>
        public override void AddDeprovisoningSteps(SetupController<NodeDefinition> controller)
        {
            throw new NotImplementedException("$todo(jefflill): Implement this.");
        }

        /// <inheritdoc/>
        public override (string Address, int Port) GetSshEndpoint(string nodeName)
        {
            return (Address: cluster.GetNode(nodeName).Address.ToString(), Port: NetworkPorts.SSH);
        }

        /// <inheritdoc/>
        public override string GetDataDisk(LinuxSshProxy node)
        {
            Covenant.Requires<ArgumentNullException>(node != null, nameof(node));

            // This hosting manager doesn't currently provision a separate data disk.

            return "PRIMARY";
        }
    }
}
