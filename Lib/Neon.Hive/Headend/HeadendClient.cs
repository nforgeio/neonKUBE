//-----------------------------------------------------------------------------
// FILE:	    HeadendClient.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Consul;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Neon.Common;
using Neon.Docker;
using Neon.IO;
using Neon.Net;
using Neon.Retry;
using Neon.Time;

namespace Neon.Hive
{
    // $todo(jeff.lill):
    //
    // I'm just hardcoding this for now so that I can complete client 
    // side coding.  I'll flesh this out when I actually implement the
    // headend services.

    /// <summary>
    /// Provides access to neonHIVE headend services.
    /// </summary>
    public sealed class HeadendClient : IDisposable
    {
        private JsonClient jsonClient;

        /// <summary>
        /// Constructor.
        /// </summary>
        public HeadendClient()
        {
            jsonClient = new JsonClient();
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            jsonClient.Dispose();
        }

        /// <summary>
        /// Returns the latest versions of neonHIVE components and services
        /// that is compatible with a specific version of a neonHIVE deployment.
        /// </summary>
        /// <param name="hiveVersion">The current hive version.</param>
        /// <returns>The <see cref="HiveComponentVersions"/>.</returns>
        public HiveComponentVersions GetComponentVersions(string hiveVersion)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(hiveVersion));

            var versions = new HiveComponentVersions();

            versions.Docker        = "18.06.1-ce";
            versions.DockerPackage = "docker-ce=18.03.1~ce-0~ubuntu";

            versions.Consul        = "1.1.0";
            versions.Vault         = "0.10.3";

            var systemContainers = HiveConst.DockerContainers;
            var systemServices   = HiveConst.DockerServices;

            // $todo(jeff.lill): Hardcoded

            foreach (var container in systemContainers)
            {
                versions.Images[container] = $"nhive/{container}:latest";
            }

            foreach (var service in systemServices)
            {
                versions.Images[service] = $"nhive/{service}:latest";
            }

            return versions;
        }

        /// <summary>
        /// Converts a Docker version number into the fully qualified Debian
        /// package name.
        /// </summary>
        /// <param name="version">The Docker version.</param>
        /// <param name="message">Returns as an informational message.</param>
        /// <returns>The package name or <c>null</c> if no package exists for the version.</returns>
        public string GetDockerPackage(string version, out string message)
        {
            // $todo(jeff.lill): Hardcoded

            message = "ok";

            // Strip the "-ce" suffix.

            version = version.Replace("-ce", string.Empty);

            return $"docker-ce={version}~ce-0~ubuntu";
        }

        /// <summary>
        /// Determines whether a specific Docker version is available and is compatible
        /// with a neonHIVE.
        /// </summary>
        /// <param name="hiveVersion">The current hive version.</param>
        /// <param name="dockerVersion">The Docker version being tested.</param>
        /// <param name="message">Returns as an information message.</param>
        /// <returns><c>true</c> if the Docker version is compatible.</returns>
        public bool IsDockerCompatible(string hiveVersion, string dockerVersion, out string message)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(hiveVersion));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(dockerVersion));

            // $todo(jeff.lill): Hardcoded

            message = "compatible";

            return true;
        }

        /// <summary>
        /// Determines whether a specific HashiCorp Cobsul version is available and is compatible
        /// with a neonHIVE.
        /// </summary>
        /// <param name="hiveVersion">The current hive version.</param>
        /// <param name="consulVersion">The Cobsul version being tested.</param>
        /// <param name="message">Returns as an information message.</param>
        /// <returns><c>true</c> if the Docker version is compatible.</returns>
        public bool IsConsulCompatible(string hiveVersion, string consulVersion, out string message)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(hiveVersion));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(consulVersion));

            // $todo(jeff.lill): Hardcoded

            message = "compatible";

            return true;
        }

        /// <summary>
        /// Determines whether a specific HashiCorp Vault version is available and is compatible
        /// with a neonHIVE.
        /// </summary>
        /// <param name="hiveVersion">The current hive version.</param>
        /// <param name="vaultVersion">The Docker version being tested.</param>
        /// <param name="message">Returns as an information message.</param>
        /// <returns><c>true</c> if the Docker version is compatible.</returns>
        public bool IsVaultCompatible(string hiveVersion, string vaultVersion, out string message)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(hiveVersion));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(vaultVersion));

            // $todo(jeff.lill): Hardcoded

            message = "compatible";

            return true;
        }
    }
}
