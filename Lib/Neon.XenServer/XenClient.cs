//-----------------------------------------------------------------------------
// FILE:	    XenClient.cs
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
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Kube;

namespace Neon.Xen
{
    /// <summary>
    /// This class provides a simple light-weight XenServer or CXP-ng 
    /// API that connects to the XenServer host operating system via 
    /// SSH and executes commands using the <b>xe</b> XenServer client
    /// tool.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Ideally, we'd use the XenServer .NET API but at this time (Jan 2018),
    /// the API is not compatible with .NET Core which cluster <b>neon-cli</b>
    /// requires because it needs to run on Windows, OSX, and perhaps some day
    /// within the Ubuntu based tool container.
    /// </para>
    /// <para>
    /// The workaround is to simnply connect to the XenServer host via SSH
    /// and perform commands using the <b>xe</b> command line tool installed
    /// with XenServer.  We're going to take advantage of the <see cref="SshProxy{TMetadata}"/>
    /// class to handle the SSH connection and command execution.
    /// </para>
    /// <para>
    /// XenServer template operations are implemented by the <see cref="Template"/>
    /// property, storage repository operations by <see cref="Repository"/> and
    /// virtual machine operations by <see cref="Machine"/>.
    /// </para>
    /// </remarks>
    /// <threadsafety instance="false"/>
    public sealed partial class XenClient : IDisposable, IXenClient
    {
        // Implementation Note:
        // --------------------
        // The following PDF documents are handy resources for learning about the
        // XE command line tool.
        //
        //      https://docs.citrix.com/content/dam/docs/en-us/xenserver/current-release/downloads/xenserver-vm-users-guide.pdf
        //      https://docs.citrix.com/content/dam/docs/en-us/xenserver/xenserver-7-0/downloads/xenserver-7-0-management-api-guide.pdf

        private RunOptions  runOptions;

        /// <summary>
        /// Constructor.  Note that you should dispose the instance when you're finished with it.
        /// </summary>
        /// <param name="addressOrFQDN">The target XenServer IP address or FQDN.</param>
        /// <param name="username">The user name.</param>
        /// <param name="password">The password.</param>
        /// <param name="name">Optionally specifies the XenServer name.</param>
        /// <param name="logFolder">
        /// The folder where log files are to be written, otherwise or <c>null</c> or 
        /// empty if logging is disabled.
        /// </param>
        public XenClient(string addressOrFQDN, string username, string password, string name = null, string logFolder = null)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(username), nameof(username));

            if (!IPAddress.TryParse(addressOrFQDN, out var address))
            {
                var hostEntry = Dns.GetHostEntry(addressOrFQDN);

                if (hostEntry.AddressList.Length == 0)
                {
                    throw new XenException($"[{addressOrFQDN}] is not a valid IP address or fully qualified domain name of a XenServer host.");
                }

                address = hostEntry.AddressList.First();
            }

            var logWriter = (TextWriter)null;

            if (!string.IsNullOrEmpty(logFolder))
            {
                Directory.CreateDirectory(logFolder);

                logWriter = new StreamWriter(Path.Combine(logFolder, $"XENSERVER-{addressOrFQDN}.log"));
            }

            Address           = addressOrFQDN;
            Name              = name;
            SshProxy          = new SshProxy<XenClient>(addressOrFQDN, null, address, SshCredentials.FromUserPassword(username, password), logWriter);
            SshProxy.Metadata = this;
            runOptions        = RunOptions.IgnoreRemotePath;

            // Initialize the operation classes.

            Repository = new RepositoryOperations(this);
            Template   = new TemplateOperations(this);
            Machine    = new MachineOperations(this);
        }

        /// <summary>
        /// Releases any resources associated with the instance.
        /// </summary>
        public void Dispose()
        {
            if (SshProxy == null)
            {
                SshProxy.Dispose();
                SshProxy = null;
            }
        }

        /// <summary>
        /// Returns the XenServer name as passed to the constructor.
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// Returns the address or FQDN of the remote XenServer.
        /// </summary>
        public string Address { get; private set; }

        /// <summary>
        /// Returns the SSH proxy for the XenServer host.
        /// </summary>
        public SshProxy<XenClient> SshProxy { get; private set; }

        /// <summary>
        /// Implements the XenServer storage repository operations.
        /// </summary>
        public RepositoryOperations Repository { get; private set; }

        /// <summary>
        /// Implements the XenServer virtual machine template operations.
        /// </summary>
        public TemplateOperations Template { get; private set; }

        /// <summary>
        /// Implements the XenServer virtual machine operations.
        /// </summary>
        public MachineOperations Machine { get; private set; }

        /// <summary>
        /// Verifies that that the instance hasn't been disposed.
        /// </summary>
        private void VerifyNotDisposed()
        {
            if (SshProxy == null)
            {
                throw new ObjectDisposedException(nameof(XenClient));
            }
        }

        /// <summary>
        /// Invokes a low-level <b>xe CLI</b> command on the remote XenServer host
        /// that returns text.
        /// </summary>
        /// <param name="command">The <b>xe CLI</b> command.</param>
        /// <param name="args">The optional arguments formatted as <b>name=value</b>.</param>
        /// <returns>The command response.</returns>
        public CommandResponse Invoke(string command, params string[] args)
        {
            VerifyNotDisposed();
            return SshProxy.RunCommand($"xe {command}", runOptions, args);
        }

        /// <summary>
        /// Invokes a low-level <b>xe CLI</b> command on the remote XenServer host
        /// that returns a list of items.
        /// </summary>
        /// <param name="command">The <b>xe CLI</b> command.</param>
        /// <param name="args">The optional arguments formatted as <b>name=value</b>.</param>
        /// <returns>The command <see cref="XenResponse"/>.</returns>
        public XenResponse InvokeItems(string command, params string[] args)
        {
            VerifyNotDisposed();
            return new XenResponse(SshProxy.RunCommand($"xe {command}", runOptions, args));
        }

        /// <summary>
        /// Invokes a low-level <b>xe CLI</b> command on the remote XenServer host
        /// that returns text, throwing an exception on failure.
        /// </summary>
        /// <param name="command">The <b>xe CLI</b> command.</param>
        /// <param name="args">The optional arguments formatted as <b>name=value</b>.</param>
        /// <returns>The command response.</returns>
        /// <exception cref="XenException">Thrown if the operation failed.</exception>
        public CommandResponse SafeInvoke(string command, params string[] args)
        {
            VerifyNotDisposed();

            var response = SshProxy.RunCommand($"xe {command}", runOptions, args);

            if (response.ExitCode != 0)
            {
                throw new XenException($"XE-COMMAND: {command} MESSAGE: {response.ErrorText}");
            }

            return response;
        }

        /// <summary>
        /// Invokes a low-level <b>xe CLI</b> command on the remote XenServer host
        /// that returns a list of items, throwing an exception on failure.
        /// </summary>
        /// <param name="command">The <b>xe CLI</b> command.</param>
        /// <param name="args">The optional arguments formatted as <b>name=value</b>.</param>
        /// <returns>The command <see cref="XenResponse"/>.</returns>
        /// <exception cref="XenException">Thrown if the operation failed.</exception>
        public XenResponse SafeInvokeItems(string command, params string[] args)
        {
            return new XenResponse(SafeInvoke(command, args));
        }
    }
}
