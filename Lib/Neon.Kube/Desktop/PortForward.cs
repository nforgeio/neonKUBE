//-----------------------------------------------------------------------------
// FILE:	    PortForward.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:	Copyright (c) 2016-2020 by neonFORGE, LLC.  All rights reserved.
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
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Net.Http.Server;

using Neon.Common;
using Neon.Net;

namespace Neon.Kube
{
	/// <summary>
	/// <para>
	/// Implements a reverse HTTP or proxy between an endpoint on the local machine
	/// and an endpoint on a remote machine.
	/// </para>
	/// <note>
	/// This is supported <b>only on Windows</b>.
	/// </note>
	/// </summary>
	/// <remarks>
	/// <para>
	/// Use the <see cref="PortForward"/> constructor to create a proxy.  You'll
	/// pass the local and remote endpoints and optional request and response 
	/// handlers.
	/// </para>
	/// <para>
	/// The request handler will be called when a request is received on the local
	/// endpoint give the handler a chance to modify the request before it is
	/// forwarded on to the remote endpoint.  The response handler is called when
	/// a response is received from the remote endpoint, giving the handler a
	/// chance to examine and possibly modify the response before it is returned
	/// to the caller.
	/// </para>
	/// </remarks>
	public sealed class PortForward : IDisposable
	{
		//---------------------------------------------------------------------
		// Static members


		/// <summary>
		/// Static constructor.
		/// </summary>
		static PortForward()
		{
		}

		//---------------------------------------------------------------------
		// Instance members

		private object                  syncLock = new object();
		private string                  serviceName;
		private int                     localPort;
		private int                     remotePort;
		private string                  @namespace;

		// I was unable to get [PortForward] request forwarding to the
		// Kubernetes dashboard to work.  I believe it's a problem with
		// the self-signed certificate generated for the dashboard.  The
		// HTTP handler's certificate validation function is never called
		// either.
		//
		// I'm going to work around this by launching [kubectl proxy]
		// to manage the dashboard proxy, setting this to the process,
		// and then configuring the reverse proxy so that it forwards
		// requests to [kubectl proxy] which then forwards them on to
		// the dashboard service in the cluster.
		//
		// We still need the reverse proxy so we can inject a token
		// via an authentication header.

		private Process kubectlProxyProcess;

		/// <summary>
		/// Constructs a port forward.
		/// </summary>
		/// <param name="serviceName">The service to connect to.</param>
		/// <param name="localPort">The local port.</param>
		/// <param name="remotePort">The remote port.</param>
		/// <param name="namespace">The namespace which the service is running.</param>
		/// Optionally specifies an acceptable server certificate.  This can be used 
		/// as a way to allow access for a specific self-signed certificate.
		public PortForward(
			string      serviceName,
			int         localPort,
			int         remotePort,
			string      @namespace = "default")
		{
			Covenant.Requires<ArgumentException>(NetHelper.IsValidPort(localPort), nameof(localPort));
			Covenant.Requires<ArgumentException>(NetHelper.IsValidPort(remotePort), nameof(remotePort));

			if (!NeonHelper.IsWindows)
			{
				throw new NotSupportedException($"[{nameof(PortForward)}] is supported only on Windows.");
			}

			this.serviceName         = serviceName;
			this.localPort           = localPort;
			this.remotePort          = remotePort;
			this.@namespace          = @namespace;
			this.kubectlProxyProcess = new Process();

			// Create the client.

			KubeHelper.PortForward(serviceName, remotePort, localPort, @namespace, kubectlProxyProcess);
		}

		/// <ingeritdoc/>
		public void Dispose()
		{
			lock (syncLock)
			{
				
				if (kubectlProxyProcess != null)
				{
					kubectlProxyProcess.Kill();
					kubectlProxyProcess = null;
				}
			}
		}
	}
}
