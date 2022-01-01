//-----------------------------------------------------------------------------
// FILE:	    DockerSettings.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.
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
using System.Text;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Net;
using Neon.Retry;

namespace Neon.Docker
{
    /// <summary>
    /// Specifies the configuration settings for a <see cref="DockerClient"/>.
    /// </summary>
    public class DockerSettings
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        private DockerSettings()
        {
            this.RetryPolicy = new ExponentialRetryPolicy(TransientDetector.NetworkOrHttp);
        }

        /// <summary>
        /// Constructs settings using a DNS hostname for the Docker engine.
        /// </summary>
        /// <param name="host">Engine hostname.</param>
        /// <param name="port">Optional TCP port (defaults to <see cref="NetworkPorts.Docker"/> [<b>2375</b>]).</param>
        /// <param name="secure">Optionally specifies that the connection will be secured via TLS (defaults to <c>false</c>).</param>
        public DockerSettings(string host, int port = NetworkPorts.Docker, bool secure = false)
            : this()
        {
            var scheme = secure ? "https" : "http";

            this.Uri = new Uri($"{scheme}://{host}:{port}");
        }

        /// <summary>
        /// Constructs settings using an <see cref="IPAddress"/> for the Docker engine.
        /// </summary>
        /// <param name="address">The engine IP address.</param>
        /// <param name="port">Optional TCP port (defaults to <see cref="NetworkPorts.Docker"/> [<b>2375</b>]).</param>
        /// <param name="secure">Optionally specifies that the connection will be secured via TLS (defaults to <c>false</c>).</param>
        public DockerSettings(IPAddress address, int port = NetworkPorts.Docker, bool secure = false)
            : this(address.ToString(), port, secure)
        {
        }

        /// <summary>
        /// Constructs settings from a URI.  Note that you may specify a Unix domain
        /// socket like: <b>unix:///var/run/docker/sock</b>.
        /// </summary>
        /// <param name="uri">The URI.</param>
        public DockerSettings(string uri)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(uri), nameof(uri));

            this.Uri = new Uri(uri);
        }

        /// <summary>
        /// Returns the target engine's base URI.
        /// </summary>
        public Uri Uri { get; private set; }

        /// <summary>
        /// The <see cref="IRetryPolicy"/> to be used when submitting requests to docker.
        /// This defaults to a reasonable <see cref="ExponentialRetryPolicy"/> using the
        /// <see cref="TransientDetector.NetworkOrHttp(Exception)"/> transient detector.
        /// </summary>
        public IRetryPolicy RetryPolicy { get; set; }

        /// <summary>
        /// Creates a <see cref="DockerClient"/> using the settings.
        /// </summary>
        /// <returns>The created <see cref="DockerClient"/>.</returns>
        public DockerClient CreateClient()
        {
            return new DockerClient(this);
        }
    }
}
