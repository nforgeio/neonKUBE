//-----------------------------------------------------------------------------
// FILE:	    XenClient.cs
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
using System.Collections.ObjectModel;
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
using Neon.Diagnostics;
using Neon.IO;
using Neon.Kube;
using Neon.Net;
using Neon.SSH;

namespace Neon.XenServer
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
    /// with XenServer.  We're going to take advantage of the <see cref="NodeSshProxy{TMetadata}"/>
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
        /// <summary>
        /// Path to the parent folder for all temporary Neon local storage repositories.
        /// </summary>
        public const string NeonTempSrPath = "/var/opt/neon-temp-sr";

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

            if (!NetHelper.TryParseIPv4Address(addressOrFQDN, out var address))
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
            SshProxy          = new NodeSshProxy<XenClient>(addressOrFQDN, address, SshCredentials.FromUserPassword(username, password), logWriter: logWriter);
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
        public NodeSshProxy<XenClient> SshProxy { get; private set; }

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

        /// <summary>
        /// Returns information about the connected XenServer host machine.
        /// </summary>
        /// <returns>The <see cref="XenHostInfo"/>.</returns>
        public XenHostInfo GetHostInfo()
        {
            // List the hosts to obtain the host UUID.  We're going to assume that only the
            // current host will be returned and the confguring a resource pool doesn't change
            // this.

            var response = SafeInvokeItems("host-list");

            Covenant.Assert(response.Items.Count == 1, "[xe host-list] is expected to return exactly one host.");

            response = SafeInvokeItems("host-param-list", $"uuid={response.Items.Single()["uuid"]}");

            var hostParams   = response.Items.Single();
            var versionItems = hostParams["software-version"].Split(';');

            for (int i = 0; i < versionItems.Length; i++)
            {
                versionItems[i] = versionItems[i].Trim();
            }

            var version = versionItems.Single(item => item.StartsWith("product_version:"));
            var pos     = version.IndexOf(':');

            version = version.Substring(pos + 1).Trim();

            return new XenHostInfo()
            {
                Edition = hostParams["edition"],
                Version = SemanticVersion.Parse(version),
                Params  = new ReadOnlyDictionary<string, string>(hostParams)
            };
        }

        /// <summary>
        /// Used for temporarily uploading an ISO disk to a XenServer such that it can be mounted
        /// to a VM, typically for one-time initialization purposes.  neonKUBE uses this as a very
        /// simple poor man's alternative to <b>cloud-init</b> for initializing a VM on first boot.
        /// </summary>
        /// <param name="isoPath">Path to the source ISO file on the local workstation.</param>
        /// <param name="srName">Optionally specifies the storage repository name.  <b>neon-UUID</b> with a generated UUID will be used by default.</param>
        /// <returns>A <see cref="XenTempIso"/> with information about the new storage repository and its contents.</returns>
        /// <remarks>
        /// <para>
        /// During cluster setup on virtualization platforms like XenServer and Hyper-V, neonKUBE needs
        /// to configure new VMs with IP addresses, hostnames, etc.  Traditionally, we've relied on
        /// being able to SSH into the VM to perform all of these actions, but this relied on being
        /// VM being able to obtain an IP address via DHCP and for setup to be able to discover the
        /// assigned address.
        /// </para>
        /// <para>
        /// The dependency on DHCP is somewhat problematic, because it's conceivable that this may
        /// not be available for more controlled environments.  We looked into using Linux <b>cloud-init</b>
        /// for this, but that requires additional local infrastructure for non-cloud deployments and
        /// was also a bit more complex than what we had time for.
        /// </para>
        /// <para>
        /// Instead of <b>cloud-init</b>, we provisioned our XenServer and Hyper-V node templates
        /// with a <b>neon-node-init</b> service that runs before the network service to determine
        /// whether a DVD (ISO) is inserted into the VM and runs the <b>neon-node-init.sh</b> script
        /// one time, if it exists.  This script will initialize the node's IP address and could also
        /// be used for other configuration.
        /// </para>
        /// <note>
        /// In theory, we could have used the same technique for mounting a <b>cloud-init</b> data source
        /// via this ISO, but we decided not to go there, at least for now.
        /// </note>
        /// <note>
        /// neonKUBE doesn't use this technique for true cloud deployments (AWS, Azure, Google,...) because
        /// we can configure VM networking directly via the cloud APIs.  
        /// </note>
        /// <para>
        /// The XenServer requires the temporary ISO implementation to be a bit odd.  We want these temporary
        /// ISOs to be created directly on the XenServer host machine so users won't have to configure any
        /// additional infrastructure as well as to simplify cluster setup.  We'll be creating a local
        /// ISO storage repository from a folder on the host.  Any files to be added to the repository
        /// must exist when the repository is created and it is not possible to add, modify, or remove
        /// files from a repository after its been created.
        /// </para>
        /// <note>
        /// XenServer hosts have only 4GB of free space at the root Linux level, so you must take care 
        /// not to create large ISOs or to allow these to accumulate.
        /// </note>
        /// <para>
        /// This method uploads the ISO file <paramref name="isoPath"/> from the local workstation to
        /// the XenServer host, creating a new folder named with a UUID.  Then a new storage repository
        /// will be created from this folder and a <see cref="XenTempIso"/> will be returned holding
        /// details about the new storage repository and its contents.  The setup code will use this to 
        /// insert the ISO into a VM.
        /// </para>
        /// <para>
        /// Once the setup code is done with the ISO, it will eject it from the VM and call
        /// <see cref="RemoveTempIso(XenTempIso)"/> to remove the storage repository.
        /// </para>
        /// </remarks>
        public XenTempIso CreateTempIso(string isoPath, string srName = null)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(isoPath), nameof(isoPath));

            if (string.IsNullOrEmpty(srName))
            {
                srName = "neon-" + Guid.NewGuid().ToString("d");
            }

            var tempIso = new XenTempIso();

            // Ensure that the root temporary local SR folder exists.

            SshProxy.SudoCommand("mkdir", RunOptions.LogOutput, NeonTempSrPath);

            // Create the SR subfolder and upload the ISO file.

            tempIso.SrPath = LinuxPath.Combine(NeonTempSrPath, Guid.NewGuid().ToString("d"));
            tempIso.CdName = $"neon-dvd-{Guid.NewGuid().ToString("d")}.iso";

            SshProxy.SudoCommand("mkdir", RunOptions.LogOutput | RunOptions.FaultOnError, tempIso.SrPath);

            var xenIsoPath = LinuxPath.Combine(tempIso.SrPath, tempIso.CdName);

            using (var isoInput = File.OpenRead(isoPath))
            {
                SshProxy.Upload(xenIsoPath, isoInput);
            }

            // Create the new storage repository.  This command returns the sr-uuid.

            var response = SafeInvoke("sr-create",
                $"name-label={tempIso.CdName}",
                $"type=iso",
                $"device-config:location={tempIso.SrPath}",
                $"device-config:legacy_mode=true",
                $"content-type=iso");

            tempIso.SrUuid = response.OutputText.Trim();

            // XenServer created a PBD behind the scenes for the new SR.  We're going
            // to need its UUID so we can completely remove the SR later.

            var result = SafeInvokeItems("pbd-list", $"sr-uuid={tempIso.SrUuid}");

            tempIso.PdbUuid = result.Items.Single()["uuid"];

            // Obtain the UUID for the ISO's VDI within the SR.

            result = SafeInvokeItems("vdi-list", $"sr-uuid={tempIso.SrUuid}");

            tempIso.VdiUuid = result.Items.Single()["uuid"];

            return tempIso;
        }

        /// <summary>
        /// Removes a temporary ISO disk along with its PBD and storage repository.
        /// </summary>
        /// <param name="tempIso">The ISO disk information returned by <see cref="CreateTempIso(string, string)"/>.</param>
        /// <remarks>
        /// <see cref="CreateTempIso(string, string)"/> for more information.
        /// </remarks>
        public void RemoveTempIso(XenTempIso tempIso)
        {
            Covenant.Requires<ArgumentNullException>(tempIso != null, nameof(tempIso));

            // Remove the PBD and SR.

            SafeInvoke("pbd-unplug", $"uuid={tempIso.PdbUuid}");
            SafeInvoke("sr-forget", $"uuid={tempIso.SrUuid}");

            // Remove the SR folder.

            SshProxy.SudoCommand("rm", "-rf", tempIso.SrPath);
        }
    }
}
