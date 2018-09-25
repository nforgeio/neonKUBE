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
        /// <returns>The <see cref="HiveComponentInfo"/>.</returns>
        public HiveComponentInfo GetComponentInfo(string hiveVersion)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(hiveVersion));

            var versions = new HiveComponentInfo();

            versions.Docker        = "18.06.1-ce";
            versions.DockerPackage = "docker-ce=18.03.1~ce-0~ubuntu";

            versions.Consul        = "1.1.0";
            versions.Vault         = "0.10.3";

            var systemContainers = HiveConst.DockerContainers;
            var systemServices   = HiveConst.DockerServices;

            // $todo(jeff.lill): Hardcoded for now.

            versions.ComponentToImage.Add("neon-dns",                   "neon-dns");
            versions.ComponentToImage.Add("neon-dns-mon",               "neon-dns-mon");
            versions.ComponentToImage.Add("neon-hivemq",                "neon-hivemq");
            versions.ComponentToImage.Add("neon-hive-manager",          "neon-hive-manager");
            versions.ComponentToImage.Add("neon-log-esdata",            "elasticsearch");
            versions.ComponentToImage.Add("neon-log-collector",         "neon-log-collector");
            versions.ComponentToImage.Add("neon-log-kibana",            "kibana");
            versions.ComponentToImage.Add("neon-log-host",              "neon-log-host");
            versions.ComponentToImage.Add("neon-log-metricbeat",        "metricbeat");
            versions.ComponentToImage.Add("neon-proxy-manager",         "neon-proxy-manager");
            versions.ComponentToImage.Add("neon-proxy-private",         "neon-proxy");
            versions.ComponentToImage.Add("neon-proxy-public",          "neon-proxy");
            versions.ComponentToImage.Add("neon-proxy-vault",           "neon-proxy-vault");
            versions.ComponentToImage.Add("neon-registry-cache",        "neon-registry-cache");

            versions.ImageToFullyQualified.Add("elasticsearch",         "nhive/elasticsearch:latest");
            versions.ImageToFullyQualified.Add("kibana",                "nhive/kibana:latest");
            versions.ImageToFullyQualified.Add("metricbeat",            "nhive/metricbeat:latest");
            versions.ImageToFullyQualified.Add("neon-dns",              "nhive/neon-dns:latest");
            versions.ImageToFullyQualified.Add("neon-dns-mon",          "nhive/neon-dns-mon:latest");
            versions.ImageToFullyQualified.Add("neon-hivemq",           "nhive/neon-hivemq:latest");
            versions.ImageToFullyQualified.Add("neon-hive-manager",     "nhive/neon-hive-manager:latest");
            versions.ImageToFullyQualified.Add("neon-log-collector",    "nhive/neon-log-collector:latest");
            versions.ImageToFullyQualified.Add("neon-log-host",         "nhive/neon-log-host:latest");
            versions.ImageToFullyQualified.Add("neon-proxy",            "nhive/neon-proxy:latest");
            versions.ImageToFullyQualified.Add("neon-proxy-manager",    "nhive/neon-proxy-manager:latest");
            versions.ImageToFullyQualified.Add("neon-proxy-vault",      "nhive/neon-proxy-vault:latest");
            versions.ImageToFullyQualified.Add("neon-registry-cache",   "nhive/neon-registry-cache:latest");

            // Ensure that every system component has an image assignment.

            foreach (var component in systemContainers.Union(systemServices))
            {
                if (!versions.ComponentToImage.ContainsKey(component))
                {
                    throw new NotImplementedException($"Hive container or service name [{component}] does not have an image assignment.");
                }
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
