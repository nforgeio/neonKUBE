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
        /// <param name="branch">The Git branch used to build the client.</param>
        /// <returns>The <see cref="HiveComponentInfo"/>.</returns>
        public HiveComponentInfo GetComponentInfo(string hiveVersion, string branch)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(hiveVersion));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(branch));
            Covenant.Requires<ArgumentException>(branch.IndexOf(' ') == -1);

            var versions = new HiveComponentInfo();

            versions.Docker           = "18.09.0-ce";
            versions.DockerPackageUri = GetDockerPackageUri(versions.Docker, out var stub);

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
            versions.ComponentToImage.Add("neon-proxy-public-cache",    "neon-proxy-cache");
            versions.ComponentToImage.Add("neon-proxy-private-cache",   "neon-proxy-cache");
            versions.ComponentToImage.Add("neon-proxy-vault",           "neon-proxy-vault");
            versions.ComponentToImage.Add("neon-registry",              "neon-registry");
            versions.ComponentToImage.Add("neon-registry-cache",        "neon-registry-cache");

            var repoOrg = branch.Equals("prod", StringComparison.InvariantCultureIgnoreCase) ? "nhive" : "nhivedev";
            var repoTag = branch.Equals("prod", StringComparison.InvariantCultureIgnoreCase) ? "latest" : $"{branch.ToLowerInvariant()}-latest";

            versions.ImageToFullyQualified.Add("elasticsearch",         $"{repoOrg}/elasticsearch:{repoTag}");
            versions.ImageToFullyQualified.Add("kibana",                $"{repoOrg}/kibana:{repoTag}");
            versions.ImageToFullyQualified.Add("metricbeat",            $"{repoOrg}/metricbeat:{repoTag}");
            versions.ImageToFullyQualified.Add("neon-dns",              $"{repoOrg}/neon-dns:{repoTag}");
            versions.ImageToFullyQualified.Add("neon-dns-mon",          $"{repoOrg}/neon-dns-mon:{repoTag}");
            versions.ImageToFullyQualified.Add("neon-hivemq",           $"{repoOrg}/neon-hivemq:{repoTag}");
            versions.ImageToFullyQualified.Add("neon-hive-manager",     $"{repoOrg}/neon-hive-manager:{repoTag}");
            versions.ImageToFullyQualified.Add("neon-log-collector",    $"{repoOrg}/neon-log-collector:{repoTag}");
            versions.ImageToFullyQualified.Add("neon-log-host",         $"{repoOrg}/neon-log-host:{repoTag}");
            versions.ImageToFullyQualified.Add("neon-proxy",            $"{repoOrg}/neon-proxy:{repoTag}");
            versions.ImageToFullyQualified.Add("neon-proxy-cache",      $"{repoOrg}/neon-proxy-cache:{repoTag}");
            versions.ImageToFullyQualified.Add("neon-proxy-manager",    $"{repoOrg}/neon-proxy-manager:{repoTag}");
            versions.ImageToFullyQualified.Add("neon-proxy-vault",      $"{repoOrg}/neon-proxy-vault:{repoTag}");
            versions.ImageToFullyQualified.Add("neon-registry",         $"{repoOrg}/neon-registry:{repoTag}");
            versions.ImageToFullyQualified.Add("neon-registry-cache",   $"{repoOrg}/neon-registry-cache:{repoTag}");

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
        /// Converts a Docker version number into the fully qualified Debian package download URI.
        /// </summary>
        /// <param name="version">The Docker version (like <b>18.06.1-ce</b>) or <b>latest</b>.</param>
        /// <param name="message">Returns as an informational message.</param>
        /// <returns>The package URI or <c>null</c> if no package exists for the version.</returns>
        public string GetDockerPackageUri(string version, out string message)
        {
            // $todo(jeff.lill): Hardcoded

            message = "ok";

            if (version == "latest")
            {
                version = "18.09.0-ce";
            }

            switch (version)
            {
                case "18.03.0-ce":  
                case "18.03.1-ce": 
                case "18.06.0-ce":  
                case "18.06.1-ce":  
                case "18.09.0-ce":

                    return $"https://s3.us-west-2.amazonaws.com/neonforge/neoncluster/docker-{version}-ubuntu-xenial-stable-amd64.deb";

                default:

                    message = $"Unknown or supported Docker version [{version}].";

                    return null;
            }
        }

        /// <summary>
        /// Determines whether a specific Docker version is available and is compatible
        /// with a neonHIVE version.
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
        /// Determines whether a specific HashiCorp Consul version is available and is compatible
        /// with a neonHIVE version.
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
        /// with a neonHIVE version.
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
