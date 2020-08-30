//-----------------------------------------------------------------------------
// FILE:	    HostingManager.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2020 by neonFORGE, LLC.  All rights reserved.
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
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Cryptography;
using Neon.IO;
using Neon.Net;
using Neon.Time;

namespace Neon.Kube
{
    /// <summary>
    /// Base class for environment specific hosting managers. 
    /// </summary>
    public abstract class HostingManager : IHostingManager
    {
        /// <summary>
        /// Finalizer.
        /// </summary>
        ~HostingManager()
        {
            Dispose(false);
        }

        /// <summary>
        /// Releases any important resources associated with the instance.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
        }

        /// <summary>
        /// Releases any important resources associated with the instance.
        /// </summary>
        /// <param name="disposing">Pass <c>true</c> if the instance is being disposed as opposed to being finalized.</param>
        public abstract void Dispose(bool disposing);

        /// <summary>
        /// Specifies whether the class should print setup status to the console.
        /// This defaults to <c>false</c>.
        /// </summary>
        public bool ShowStatus { get; set; } = false;

        /// <summary>
        /// The maximum number of nodes that will execute provisioning steps in parallel.  This
        /// defaults to <b>5</b>.
        /// </summary>
        public int MaxParallel { get; set; } = 5;

        /// <summary>
        /// Number of seconds to delay after specific operations (e.g. to allow services to stablize).
        /// This defaults to <b>0.0</b>.
        /// </summary>
        public double WaitSeconds { get; set; } = 0.0;

        /// <inheritdoc/>
        public virtual bool IsProvisionNOP
        {
            get { return false; }
        }

        /// <inheritdoc/>
        public abstract void Validate(ClusterDefinition clusterDefinition);

        /// <inheritdoc/>
        public virtual bool RequiresAdminPrivileges => true;

        /// <inheritdoc/>
        public virtual bool GenerateSecurePassword => true;

        /// <inheritdoc/>
        public abstract bool Provision(bool force, string secureSshPassword, string orgSshPassword = null);

        /// <inheritdoc/>
        public virtual void AddPostProvisionSteps(SetupController<NodeDefinition> controller)
        {
        }

        /// <inheritdoc/>
        public virtual bool CanManageRouter => false;

        /// <inheritdoc/>
        public virtual void UpdatePublicIngress()
        {
        }

        /// <inheritdoc/>
        public virtual void EnablePublicSsh()
        {
        }

        /// <inheritdoc/>
        public virtual void DisablePublicSsh()
        {
        }

        /// <inheritdoc/>
        public abstract (string Address, int Port) GetSshEndpoint(string nodeName);

        /// <inheritdoc/>
        public abstract string GetDataDisk(SshProxy<NodeDefinition> node);
    }
}
