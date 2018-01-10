//-----------------------------------------------------------------------------
// FILE:	    XenClient.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2017 by neonFORGE, LLC.  All rights reserved.

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
        /// <param name="address">The target XenServer IP address.</param>
        /// <param name="password">The password.</param>
        /// <param name="username">Optionally override the default username (<b>root</b>).</param>
        public XenClient(IPAddress address, string password, string username = "root")
        {
            var addressString = address.ToString();

            server = new NodeProxy<object>(addressString, addressString, address, SshCredentials.FromUserPassword(username, password));
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


    }
}
