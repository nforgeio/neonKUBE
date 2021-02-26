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
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Diagnostics;
using Neon.IO;
using Neon.Net;
using Neon.SSH;

using Renci.SshNet;

//-----------------------------------------------------------------------------
// IMPLEMENTATION NOTE:
//
// This class originally used [LinuxSshProxy] to SSH into XenServer host machines
// and use the native [xe] client there to manage the hosts and VMs.  This worked
// relatively well, but there were some issues:
//
//      * [LinuxSshProxy] modifies some host Linux settings including disabling
//        SUDO and writing some config proxy related config files.  This will 
//        probably cause some eyebrow raising amongst serious security folks.
//
//      * There are some operations we can't perform like importing a VM template
//        that needs to be downloaded in pieces and reassembled (to stay below
//        GitHub Releases 2GB artifact file limit).  We also can't export an
//        template XVA file to the controlling computer because there's not 
//        enough disk space on the XenServer host file system (I believe it's
//        limited to 4GB total).  If there was enough space, we could extract
//        the template to the local XenServer filesystem and then used SFTP to
//        download the file to the control computer.  But this won't work.
//
// I discovered that XenXenter and XCP-ng Center both include a small Windows
// version of [xe.exe] that work's great.  This will be a simple drop-in for
// the code below.  All we'll need to do is drop [LinuxSshProxy] and then
// embed and call the [xe.exe] directly, passing the host name and user credentials.
//
// This is a temporary fix though because it won't work on OS/X or (eventually)
// Linux when we port neonDESKTOP to those platforms.  We'll need to see if we
// can build or obtain [xe] for these platforms or convert the code below to
// use the XenServer SDK (C# bindings).  This is being tracked here:
//
//      https://github.com/nforgeio/neonKUBE/issues/1130
//      https://github.com/nforgeio/neonKUBE/issues/1132

// NOTE: XE-CLI commands are documented here:
//
//      https://xcp-ng.org/docs/cli_reference.html#xe-command-reference

namespace Neon.XenServer
{
    /// <summary>
    /// This class provides a simple light-weight XenServer or XCP-ng 
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
    /// with XenServer.  We're going to take advantage of the SSH.NET package
    /// to handle the SSH connection and command execution.
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
        private bool            isDisposed = false;
        private SftpClient      sftpClient = null;
        private string          username;
        private string          password;
        private string          xePath;
        private string          xeFolder;
        private TextWriter      logWriter;

        // Implementation Note:
        // --------------------
        // The following PDF documents are handy resources for learning about the
        // XE command line tool.
        //
        //      https://docs.citrix.com/content/dam/docs/en-us/xenserver/current-release/downloads/xenserver-vm-users-guide.pdf
        //      https://docs.citrix.com/content/dam/docs/en-us/xenserver/xenserver-7-0/downloads/xenserver-7-0-management-api-guide.pdf

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
            Covenant.Requires<ArgumentNullException>(password != null, nameof(password));

            string platformSubfolder;

            if (NeonHelper.IsWindows)
            {
                platformSubfolder = "win";
            }
            else
            {
                throw new NotImplementedException($"[{nameof(XenClient)}] is currently only supported on Windows: https://github.com/nforgeio/neonKUBE/issues/113");
            }

            if (!NetHelper.TryParseIPv4Address(addressOrFQDN, out var address))
            {
                var hostEntry = Dns.GetHostEntry(addressOrFQDN);

                if (hostEntry.AddressList.Length == 0)
                {
                    throw new XenException($"[{addressOrFQDN}] is not a valid IP address or fully qualified domain name of a XenServer host.");
                }

                address = hostEntry.AddressList.First();
            }
            
            this.logWriter = (TextWriter)null;

            if (!string.IsNullOrEmpty(logFolder))
            {
                Directory.CreateDirectory(logFolder);

                this.logWriter = new StreamWriter(Path.Combine(logFolder, $"XENSERVER-{addressOrFQDN}.log"));
            }

            this.Address   = addressOrFQDN;
            this.username  = username;
            this.password  = password;
            this.Name      = name;
            this.xePath    = Path.Combine(NeonHelper.GetAssemblyFolder(Assembly.GetExecutingAssembly()), "assets-Neon.XenServer", platformSubfolder, "xe.exe");
            this.xeFolder  = Path.GetDirectoryName(xePath);

            // Connect via SFTP.

            this.sftpClient = new SftpClient(addressOrFQDN, username, password);
            this.sftpClient.Connect();

            // Initialize the operation classes.

            this.Repository = new RepositoryOperations(this);
            this.Template   = new TemplateOperations(this);
            this.Machine    = new MachineOperations(this);
        }

        /// <summary>
        /// Releases any resources associated with the instance.
        /// </summary>
        public void Dispose()
        {
            if (sftpClient != null)
            {
                sftpClient.Dispose();
                sftpClient = null;
            }

            if (logWriter != null)
            {
                logWriter.Close();
                logWriter = null;
            }

            isDisposed = true;
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
        private void EnsureNotDisposed()
        {
            if (isDisposed)
            {
                throw new ObjectDisposedException(nameof(XenClient));
            }
        }

        /// <summary>
        /// Adds the host and credential arguments to the command and arguments passed.
        /// </summary>
        /// <param name="command">The XE command.</param>
        /// <param name="args">The command arguments.</param>
        /// <returns>
        /// The complete set of arguments to the <b>xe</b> command including the host,
        /// credentials, command and command arguments.
        /// </returns>
        private string[] NormalizeArgs(string command, string[] args)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(command), nameof(command));

            var allArgs = new List<string>();

            allArgs.Add("-s"); allArgs.Add(Address);
            allArgs.Add("-u"); allArgs.Add(username);
            allArgs.Add("-pw"); allArgs.Add(password);
            allArgs.Add(command);

            if (args != null)
            {
                foreach (var arg in args)
                {
                    allArgs.Add(arg);
                }
            }

            return allArgs.ToArray();
        }

        /// <summary>
        /// Logs an XE command execution.
        /// </summary>
        /// <param name="command">The <b>XE-CLI</b> command.</param>
        /// <param name="args">The command arguments.</param>
        /// <param name="response">The command response.</param>
        /// <returns>The <paramref name="response"/>.</returns>
        private ExecuteResponse LogXeCommand(string command, string[] args, ExecuteResponse response)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(command), nameof(command));
            Covenant.Requires<ArgumentNullException>(response != null, nameof(response));

            if (logWriter == null)
            {
                return response;
            }

            args = args ?? new string[0];

            logWriter.WriteLine($"START: xe -h {Address} -u {username} -p [REDACTED] {command} {NeonHelper.NormalizeExecArgs(args)}");

            if (response.ExitCode != 0)
            {
                logWriter.WriteLine("STDOUT");

                using (var reader = new StringReader(response.OutputText))
                {
                    foreach (var line in reader.Lines())
                    {
                        logWriter.WriteLine("    " + line);
                    }
                }

                if (!string.IsNullOrEmpty(response.ErrorText))
                {
                    logWriter.WriteLine("STDERR");

                    using (var reader = new StringReader(response.ErrorText))
                    {
                        foreach (var line in reader.Lines())
                        {
                            logWriter.WriteLine("    " + line);
                        }
                    }
                }
            }

            if (response.ExitCode == 0)
            {
                logWriter.WriteLine("END [OK]");
            }
            else
            {
                logWriter.WriteLine($"END [ERROR={response.ExitCode}]");
            }

            logWriter.Flush();

            return response;
        }

        /// <summary>
        /// Invokes a low-level <b>XE-CLI</b> command on the remote XenServer host
        /// that returns text.
        /// </summary>
        /// <param name="command">The <b>XE-CLI</b> command.</param>
        /// <param name="args">The optional arguments formatted as <b>name=value</b>.</param>
        /// <returns>The command response.</returns>
        public ExecuteResponse Invoke(string command, params string[] args)
        {
            EnsureNotDisposed();

            return LogXeCommand(command, args, NeonHelper.ExecuteCapture(xePath, NormalizeArgs(command, args), workingDirectory: xeFolder));
        }

        /// <summary>
        /// Invokes a low-level <b>XE-CLI</b> command on the remote XenServer host
        /// that returns a list of items.
        /// </summary>
        /// <param name="command">The <b>XE-CLI</b> command.</param>
        /// <param name="args">The optional arguments formatted as <b>name=value</b>.</param>
        /// <returns>The command <see cref="XenResponse"/>.</returns>
        public XenResponse InvokeItems(string command, params string[] args)
        {
            EnsureNotDisposed();

            return new XenResponse(Invoke(command, args));
        }

        /// <summary>
        /// Invokes a low-level <b>XE-CLI</b> command on the remote XenServer host
        /// that returns text, throwing an exception on failure.
        /// </summary>
        /// <param name="command">The <b>XE-CLI</b> command.</param>
        /// <param name="args">The optional arguments formatted as <b>name=value</b>.</param>
        /// <returns>The command response.</returns>
        /// <exception cref="XenException">Thrown if the operation failed.</exception>
        public ExecuteResponse SafeInvoke(string command, params string[] args)
        {
            EnsureNotDisposed();

            var response = Invoke(command, args);

            if (response.ExitCode != 0)
            {
                throw new XenException($"XE-COMMAND: {command} MESSAGE: {response.AllText}");
            }

            return response;
        }

        /// <summary>
        /// Invokes a low-level <b>XE-CLI</b> command on the remote XenServer host
        /// that returns a list of items, throwing an exception on failure.
        /// </summary>
        /// <param name="command">The <b>XE-CLI</b> command.</param>
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
        /// During cluster setup on virtualization platforms like XenServer and Hyper-V, neonKUBE need
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
        /// with a <b>neon-init</b> service that runs before the network service to determine
        /// whether a DVD (ISO) is inserted into the VM and runs the <b>neon-init.sh</b> script
        /// there one time, if it exists.  This script will initialize the node's IP address and 
        /// could also be used for other configuration as well, like setting user credentials.
        /// </para>
        /// <note>
        /// In theory, we could have used the same technique for mounting a <b>cloud-init</b> data source
        /// via this ISO, but we decided not to go there, at least for now (we couldn't get that working).
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

            // Create the temporary SR subfolder and upload the ISO file.

            tempIso.SrPath  = LinuxPath.Combine("/var/run/sr-mount", Guid.NewGuid().ToString("d"));
            tempIso.IsoName = $"neon-dvd-{Guid.NewGuid().ToString("d")}.iso";

            if (!sftpClient.PathExists(tempIso.SrPath))
            {
                sftpClient.CreateDirectory(tempIso.SrPath);
                sftpClient.ChangePermissions(tempIso.SrPath, Convert.ToInt16("751", 8));
            }

            var xenIsoPath = LinuxPath.Combine(tempIso.SrPath, tempIso.IsoName);

            using (var isoInput = File.OpenRead(isoPath))
            {
                sftpClient.UploadFile(isoInput, xenIsoPath);
                sftpClient.ChangePermissions(xenIsoPath, Convert.ToInt16("751", 8));
            }

            // Create the new storage repository.  This command returns the [sr-uuid].

            var response = SafeInvoke("sr-create",
                $"name-label={tempIso.IsoName}",
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
            // Remove the PBD and SR.

            SafeInvoke("pbd-unplug", $"uuid={tempIso.PdbUuid}");
            SafeInvoke("sr-forget", $"uuid={tempIso.SrUuid}");
        }
    }
}
