//-----------------------------------------------------------------------------
// FILE:	    KubeDownloads.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright © 2005-2023 by NEONFORGE LLC.  All rights reserved.
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
using Neon.BuildInfo;
using Neon.Collections;
using Neon.Common;
using Neon.Kube.Clients;
using Neon.Kube.ClusterDef;
using Neon.Net;
using Renci.SshNet;
using YamlDotNet.Core;

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
        /// The URI for the public AWS S3 bucket for public neonKUBE releases
        /// </summary>
        public const string NeonKubeReleaseBucketUri = "https://neonkube-release.s3.us-west-2.amazonaws.com";

        /// <summary>
        /// The URI for the public AWS S3 bucket for public neonKUBE releases
        /// </summary>
        public const string NeonKubeStageBucketUri = "https://neonkube-stage.s3.us-west-2.amazonaws.com";

        /// <summary>
        /// The URI for the cluster manifest (<see cref="ClusterManifest"/>) JSON file for the current
        /// neonKUBE cluster version.
        /// </summary>
        public const string NeonClusterManifestUri = $"{NeonKubeStageBucketUri}/manifests/neonkube-{KubeVersions.NeonKube}.json";

        /// <summary>
        /// Returns the URI for the cluster manifest for a specific neonKUBE version.
        /// </summary>
        /// <param name="version">The neonKUBE version.</param>
        /// <returns>The manifest URI.</returns>
        public static string GetNeonClusterManifestUri(string version)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(version));

            return $"{NeonKubeStageBucketUri}/cluster-manifests/neonkube-{version}.json";
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
        /// <param name="architecture">The target CPU architecture.</param>
        /// <param name="useStaged">Optionally indicates that we want the URI for the staged image rather than the release image.</param>
        /// <param name="setupDebugMode">Optionally indicates that we'll be provisioning in debug mode.</param>
        /// <param name="baseImageName">
        /// Specifies the base image file name (but not the bucket and path) when <paramref name="setupDebugMode"/><c>==true</c>.
        /// For example: <b>ubuntu-22.04.1.hyperv.amd64.vhdx.gz.manifest</b>
        /// </param>
        /// <returns>The download URI or <c>null</c>.</returns>
        /// <remarks>
        /// <note>
        /// We always return the staged base image when <paramref name="baseImageName"/> is passed.
        /// </note>
        /// </remarks>
        public static async Task<string> GetNodeImageUriAsync(
            HostingEnvironment  hostingEnvironment, 
            CpuArchitecture     architecture   = CpuArchitecture.amd64,
            bool                useStaged      = false,
            bool                setupDebugMode = false,
            string              baseImageName  = null)
        {
            if (setupDebugMode && string.IsNullOrEmpty(baseImageName))
            {
                throw new NotSupportedException($"[{KubeSetupProperty.BaseImageName}] must be passed when [{nameof(setupDebugMode)}=true].");
            }

            if (!setupDebugMode)
            {
                var bucketUri = useStaged ? NeonKubeStageBucketUri : NeonKubeReleaseBucketUri;
                var branch    = ThisAssembly.Git.Branch;
                var version   = KubeVersions.NeonKube;

                if (!branch.StartsWith("release-"))
                {
                    version = $"{version}.{branch}";
                }

                //#############################################################
                // #debug(jefflill): DELETE THIS!
                //
                // Delete this after the headend service has been redeployed with the changes for:
                //
                //      https://github.com/nforgeio/neonCLOUD/issues/323

                var extension = string.Empty;

                switch (hostingEnvironment)
                {
                    case HostingEnvironment.HyperV:

                        extension = "vhdx";
                        break;

                    case HostingEnvironment.XenServer:

                        extension = "xva";
                        break;

                    default:

                        Covenant.Assert(false, $"[{nameof(hostingEnvironment)}={hostingEnvironment}] is not available from this method.");
                        break;
                }

                switch (architecture)
                {
                    case CpuArchitecture.amd64:

                        // Supported.

                        break;

                    default:

                        Covenant.Assert(false, $"[{nameof(architecture)}={architecture}] is not supported.");
                        break;
                }

                return $"{bucketUri}/images/{hostingEnvironment.ToMemberString()}/node/neonkube-node-{version}.{hostingEnvironment.ToMemberString()}.{architecture}.{extension}.gz.manifest";

                //#############################################################

                using (var headendClient = HeadendClient.Create())
                {
                    return await headendClient.ClusterSetup.GetNodeImageManifestUriAsync(hostingEnvironment.ToMemberString(), version, architecture, useStaged, ThisAssembly.Git.Branch);
                }
            }

            switch (hostingEnvironment)
            {
                case HostingEnvironment.HyperV:

                    return $"{NeonKubeStageBucketUri}/images/hyperv/base/{baseImageName}";

                case HostingEnvironment.XenServer:

                    return $"{NeonKubeStageBucketUri}/images/xenserver/base/{baseImageName}";

                default:

                    throw new NotImplementedException($"Node images are not available for the [{hostingEnvironment}] environment.");
            }
        }

        /// <summary>
        /// Returns the default URI to be used for downloading the ready-to-go neonKUBE virtual machine image 
        /// for the current neonKUBE cluster version.  This image includes a fully deployed neon-desktop built-in
        /// single node cluster.
        /// </summary>
        /// <param name="hostingEnvironment">Specifies the hosting environment.</param>
        /// <param name="architecture">The target CPU architecture.</param>
        /// <param name="useStaged">Optionally indicates that we want the URI for the staged image rather than the release image.</param>
        /// <returns>The download URI or <c>null</c>.</returns>
        public static async Task<string> GetDesktopImageUriAsync(
            HostingEnvironment  hostingEnvironment,
            CpuArchitecture     architecture = CpuArchitecture.amd64,
            bool                useStaged    = false)
        {
            var bucketUri = useStaged ? NeonKubeStageBucketUri : NeonKubeReleaseBucketUri;
            var branch    = ThisAssembly.Git.Branch;
            var version   = KubeVersions.NeonKube;

            if (!branch.StartsWith("release-"))
            {
                version = $"{version}.{branch}";
            }

            //#################################################################
            // #debug(jefflill): DELETE THIS!
            //
            // Delete this after the headend service has been redeployed with the changes for:
            //
            //      https://github.com/nforgeio/neonCLOUD/issues/323

            var extension = string.Empty;

            switch (hostingEnvironment)
            {
                case HostingEnvironment.HyperV:

                    extension = "vhdx";
                    break;

                case HostingEnvironment.XenServer:

                    extension = "xva";
                    break;

                default:

                    Covenant.Assert(false, $"[{nameof(hostingEnvironment)}={hostingEnvironment}] is not available from this method.");
                    break;
            }

            switch (architecture)
            {
                case CpuArchitecture.amd64:

                    // Supported.

                    break;

                default:

                    Covenant.Assert(false, $"[{nameof(architecture)}={architecture}] is not supported.");
                    break;
            }

            return $"{bucketUri}/images/{hostingEnvironment.ToMemberString()}/desktop/neonkube-desktop-{version}.{hostingEnvironment.ToMemberString()}.{architecture}.{extension}.gz.manifest";

            //#################################################################

            using (var headendClient = HeadendClient.Create())
            {
                return await headendClient.ClusterSetup.GetNodeImageManifestUriAsync(hostingEnvironment.ToMemberString(), KubeVersions.NeonKube, architecture, useStaged, ThisAssembly.Git.Branch);
            }
        }
    }
}
