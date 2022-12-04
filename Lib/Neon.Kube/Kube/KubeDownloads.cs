//-----------------------------------------------------------------------------
// FILE:	    KubeDownloads.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright © 2005-2022 by NEONFORGE LLC.  All rights reserved.
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
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

using Neon.Collections;
using Neon.Common;
using Neon.Net;

namespace Neon.Kube
{
    /// <summary>
    /// Kubernetes related component download URIs.
    /// </summary>
    public static class KubeDownloads
    {
        /// <summary>
        /// The Helm binary URL for Linux.
        /// </summary>
        public static readonly string HelmLinuxUri = $"https://get.helm.sh/helm-v{KubeVersions.Helm}-linux-amd64.tar.gz";

        /// <summary>
        /// The Helm binary URL for OS/X.
        /// </summary>
        public static readonly string HelmOsxUri = $"https://get.helm.sh/helm-v{KubeVersions.Helm}-darwin-amd64.tar.gz";

        /// <summary>
        /// The Helm binary URL for Windows.
        /// </summary>
        public static readonly string HelmWindowsUri = $"https://get.helm.sh/helm-v{KubeVersions.Helm}-windows-amd64.zip";

        /// <summary>
        /// The URI for the public AWS S3 bucket where we persist cluster VM images 
        /// and other things.
        /// </summary>
        public const string NeonPublicBucketUri = "https://neon-public.s3.us-west-2.amazonaws.com";

        /// <summary>
        /// The URI for the cluster manifest (<see cref="ClusterManifest"/>) JSON file for the current
        /// neonKUBE cluster version.
        /// </summary>
        public const string NeonClusterManifestUri = NeonHelper.NeonPublicBucketUri + "/cluster-manifests/neonkube-" + KubeVersions.NeonKube + ".json";

        /// <summary>
        /// Returns the URI for the cluster manifest for a specific neonKUBE version.
        /// </summary>
        /// <param name="version">The neonKUBE version.</param>
        /// <returns>The manifest URI.</returns>
        public static string GetNeonClusterManifestUri(string version)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(version));

            return $"{NeonHelper.NeonPublicBucketUri}/cluster-manifests/neonkube-{version}.json";
        }

        /// <summary>
        /// The GitHub repository path where public node images will be published.
        /// </summary>
        public const string PublicNodeImageRepo = "nforgeio/neonKUBE-images";

        /// <summary>
        /// The GitHub repository path where pre-release node images will be published.
        /// </summary>
        public const string PrivateNodeImagesRepo = "nforgeio/neonKUBE-images-dev";

        /// <summary>
        /// Returns the default URI to be used for downloading the prepared neonKUBE node virtual machine image 
        /// for the current neonKUBE cluster version.  This is the base image we'll use for provisioning cluster
        /// virtual machines.
        /// </summary>
        /// <param name="hostingEnvironment">Specifies the hosting environment.</param>
        /// <param name="setupDebugMode">Optionally indicates that we'll be provisioning in debug mode.</param>
        /// <param name="baseImageName">
        /// Specifies the base image file name (but not the bucket and path) when <paramref name="setupDebugMode"/><c>==true</c>.
        /// For example: <b>ubuntu-22.04.1.hyperv.amd64.vhdx.gz.manifest</b>
        /// </param>
        /// <param name="architecture">The target CPU architecture.</param>
        /// <returns>The download URI or <c>null</c>.</returns>
        public static async Task<string> GetNodeImageUriAsync(
            HostingEnvironment  hostingEnvironment, 
            bool                setupDebugMode = false, 
            string              baseImageName  = null,
            CpuArchitecture     architecture   = CpuArchitecture.amd64)
        {
            var hostingEnvironmentUpper = hostingEnvironment.ToString().ToUpper();

            if (setupDebugMode && string.IsNullOrEmpty(baseImageName))
            {
                throw new NotSupportedException($"[{KubeSetupProperty.BaseImageName}] must be passed when [{nameof(setupDebugMode)}=true].");
            }

            if (!setupDebugMode)
            {
                using (var headendClient = HeadendClient.Create())
                {
                    return await headendClient.ClusterSetup.GetNodeImageManifestUriAsync(hostingEnvironment.ToMemberString(), KubeVersions.NeonKube, architecture);
                }
            }

            switch (hostingEnvironment)
            {
                case HostingEnvironment.HyperV:

                    return $"{NeonHelper.NeonPublicBucketUri}/vm-images/hyperv/base/{baseImageName}";

                case HostingEnvironment.XenServer:

                    return $"{NeonHelper.NeonPublicBucketUri}/vm-images/xenserver/base/{baseImageName}";

                default:

                    throw new NotImplementedException($"Node images are not available for the [{hostingEnvironmentUpper}] environment.");
            }
        }

        /// <summary>
        /// Returns the default URI to be used for downloading the ready-to-go neonKUBE virtual machine image 
        /// for the current neonKUBE cluster version.  This image includes a fully deployed neon-desktop built-in
        /// single node cluster.
        /// </summary>
        /// <param name="hostingEnvironment">Specifies the hosting environment.</param>
        /// <param name="architecture">The target CPU architecture.</param>
        /// <returns>The download URI or <c>null</c>.</returns>
        public static async Task<string> GetDesktopImageUriAsync(
            HostingEnvironment  hostingEnvironment,
            CpuArchitecture     architecture = CpuArchitecture.amd64)
        {
            var hostingEnvironmentUpper = hostingEnvironment.ToString().ToUpper();

            using (var headendClient = HeadendClient.Create())
            {
                switch (hostingEnvironment)
                {
                    case HostingEnvironment.HyperV:

                        return await headendClient.ClusterSetup.GetNodeImageManifestUriAsync(hostingEnvironment.ToMemberString(), KubeVersions.NeonKube, architecture);

                    default:

                        throw new NotImplementedException($"Ready-to-go desktop images are not available for the [{hostingEnvironmentUpper}] environment.");
                }
            }
        }
    }
}
