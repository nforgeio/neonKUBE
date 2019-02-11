//-----------------------------------------------------------------------------
// FILE:	    Program.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.
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
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Windows.Forms;

using Neon.Kube;

namespace WinDesktop
{
    /// <summary>
    /// Manages application state.
    /// </summary>
    public static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }

        /// <summary>
        /// Returns a <see cref="ClusterProxy"/> for the current 
        /// Kubernetes context.
        /// </summary>
        /// <returns>
        /// The <see cref="ClusterProxy"/> or <c>null</c> when not 
        /// logged into a neonHUBE cluster.</returns>
        public static ClusterProxy GetCluster()
        {
            if (KubeHelper.CurrentContext == null)
            {
                return null;
            }

            return new ClusterProxy(KubeHelper.CurrentContext, Program.CreateNodeProxy<NodeDefinition>);
        }

        /// <summary>
        /// Creates a <see cref="SshProxy{TMetadata}"/> for the specified host and server name,
        /// configuring logging and the credentials as specified by the global command
        /// line options.
        /// </summary>
        /// <param name="name">The node name.</param>
        /// <param name="publicAddress">The node's public IP address or FQDN.</param>
        /// <param name="privateAddress">The node's private IP address.</param>
        /// <param name="appendToLog">
        /// Pass <c>true</c> to append to an existing log file (or create one if necessary)
        /// or <c>false</c> to replace any existing log file with a new one.
        /// </param>
        /// <typeparam name="TMetadata">Defines the metadata type the command wishes to associate with the server.</typeparam>
        /// <returns>The <see cref="SshProxy{TMetadata}"/>.</returns>
        public static SshProxy<TMetadata> CreateNodeProxy<TMetadata>(string name, string publicAddress, IPAddress privateAddress, bool appendToLog)
            where TMetadata : class
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name));

            var sshCredentials = KubeHelper.CurrentContext.Extensions.SshCredentials; ;

            return new SshProxy<TMetadata>(name, publicAddress, privateAddress, sshCredentials);
        }
    }
}
