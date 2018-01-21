//-----------------------------------------------------------------------------
// FILE:	    XenClient.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

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

using Neon.Cluster;
using Neon.Common;

namespace Neon.Cluster.XenServer
{
    /// <summary>
    /// This class provides a simple light-weight XenServer API that
    /// connects to the the XenServer host operating system via SSH
    /// and executes commands using the <b>xe</b> XenServer client
    /// tool.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Ideally, we'd use the XenServer .NET API but at this time (Jan 2018),
    /// the API is not compatible with .NET Core which neonCLUSTER <b>neon-cli</b>
    /// requires because it needs to run on Windows, OSX, and perhaps some day
    /// within the Ubuntu based tool container.
    /// </para>
    /// <para>
    /// The workaround is to simnply connect to the XenServer host via SSH
    /// and perform commands using the <b>xe</b> command line tool installed
    /// with XenServer.  We're going to take advantage of the <see cref="NodeProxy{TMetadata}"/>
    /// class to handle the SSH connection and command execution.
    /// </para>
    /// </remarks>
    /// <threadsafety instance="false"/>
    public sealed class XenClient : IDisposable
    {
        // Implementation Note:
        // --------------------
        // The XenServer Administrator's Guide was a handy resource for learning
        // about the XE tool.
        //
        //      https://docs.citrix.com/content/dam/docs/en-us/xenserver/xenserver-7-0/downloads/xenserver-7-0-management-api-guide.pdf

        private NodeProxy<object>   server;

        /// <summary>
        /// Constructor.  Note that you should dispose the instance when you're finished with it.
        /// </summary>
        /// <param name="addressOrFQDN">The target XenServer IP address or FQDN.</param>
        /// <param name="username">The user name.</param>
        /// <param name="password">The password.</param>
        public XenClient(string addressOrFQDN, string username, string password)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(username));

            if (!IPAddress.TryParse(addressOrFQDN, out var address))
            {
                var hostEntry = Dns.GetHostEntry(addressOrFQDN);

                if (hostEntry.AddressList.Length == 0)
                {
                    throw new XenException($"[{addressOrFQDN}] is not a valid IP address or fully qualified domain name of a XenServer host.");
                }

                address = hostEntry.AddressList.First();
            }

            server = new NodeProxy<object>(addressOrFQDN, null, address, SshCredentials.FromUserPassword(username, password));
        }

        /// <summary>
        /// Releases any resources associated with the instance.
        /// </summary>
        public void Dispose()
        {
            if (server == null)
            {
                server.Dispose();
                server = null;
            }
        }

        /// <summary>
        /// Verifies that that the instance hasn't been disposed.
        /// </summary>
        private void VerifyNotDisposed()
        {
            if (server == null)
            {
                throw new ObjectDisposedException(nameof(XenClient));
            }
        }

        /// <summary>
        /// Invokes a low-level <b>xe CLI</b> command on the remote XenServer host.
        /// </summary>
        /// <param name="command">The <b>xe CLI</b> command.</param>
        /// <param name="args">The optional arguments formatted as <b>name=value</b>.</param>
        /// <returns>The command <see cref="XenResponse"/>.</returns>
        public XenResponse Invoke(string command, params string[] args)
        {
            return new XenResponse(server.RunCommand($"xe {command}", args));
        }

        /// <summary>
        /// Lists the XenServer storage repositories.
        /// </summary>
        /// <returns>The list of storage repositories.</returns>
        public List<XenStorageRepository> ListStorageRepositories()
        {
            var response     = Invoke("sr-list");
            var repositories = new List<XenStorageRepository>();

            if (response.ExitCode != 0)
            {
                return repositories;
            }

            foreach (var result in response.Results)
            {
                repositories.Add(new XenStorageRepository(result));
            }

            return repositories;
        }

        /// <summary>
        /// Finds a specific storage repository by name.
        /// </summary>
        /// <returns>The named storage repository or <c>null</c> if it doesn't exist.</returns>
        public XenStorageRepository FindStorageRepository(string name)
        {
            return ListStorageRepositories().FirstOrDefault(sr => sr.NameLabel == name);
        }
    }
}

// xe vm-import url=http://s3-us-west-2.amazonaws.com/neonforge/neoncluster/ubuntu-16.04.latest-prep.xva force=true sr-uuid=1aedccc5-8b18-4fc8-b498-e776a5ae2702