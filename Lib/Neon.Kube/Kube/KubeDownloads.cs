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
using Neon.Tasks;

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

        //############################################################################################
        // $debug(jefflill): DELETE THIS AND ANY CALLS BELOW TO THESE AFTER DEPLOYING HEADEND SERVICE!

        private static string GetNodeImageUri(
            HostingEnvironment  hostingEnvironment,
            CpuArchitecture     architecture = CpuArchitecture.amd64,
            string              stageBranch  = null)
        {
            var version   = KubeVersions.NeonKube;
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

                    Covenant.Assert(false, $"[{nameof(architecture)}={architecture}] is not supported.");
                    break;
            }

            if (!string.IsNullOrEmpty(stageBranch) && !stageBranch.StartsWith("release-"))
            {
                version = $"{version}.{stageBranch}";
            }

            return $"{KubeDownloads.NeonKubeStageBucketUri}/images/{hostingEnvironment.ToMemberString()}/node/neonkube-node-{version}.{hostingEnvironment.ToMemberString()}.{architecture}.{extension}.gz.manifest";
        }

        private static string GetDesktopImageUri(
            HostingEnvironment  hostingEnvironment,
            CpuArchitecture     architecture = CpuArchitecture.amd64,
            string              stageBranch  = null)
        {
            var version   = KubeVersions.NeonKube;
            var extension = string.Empty;

            switch (hostingEnvironment)
            {
                case HostingEnvironment.HyperV:

                    extension = "vhdx";
                    break;

                default:

                    Covenant.Assert(false, $"[{nameof(architecture)}={architecture}] is not supported.");
                    break;
            }

            if (!string.IsNullOrEmpty(stageBranch) && !stageBranch.StartsWith("release-"))
            {
                version = $"{version}.{stageBranch}";
            }

            return $"{KubeDownloads.NeonKubeStageBucketUri}/images/{hostingEnvironment.ToMemberString()}/desktop/neonkube-desktop-{version}.{hostingEnvironment.ToMemberString()}.{architecture}.{extension}.gz.manifest";
        }

        //############################################################################################

        /// <summary>
        /// Returns the URI of the download manifest for a neonKUBE node image.
        /// </summary>
        /// <param name="hostingEnvironment">Identifies the hosting environment.</param>
        /// <param name="architecture">Specifies the target CPU architecture.</param>
        /// <param name="stageBranch">
        /// To obtain the URI for a staged node image, pass this as the name of the
        /// branch from which neonKUBE libraries were built.
        /// </param>
        /// <returns>The action result.</returns>
        /// <remarks>
        /// <para>
        /// When <paramref name="stageBranch"/> is <c>null</c>, the URI for the published node
        /// image will be returned.
        /// </para>
        /// <para>
        /// Otherwise, <paramref name="stageBranch"/> should be passed as the name of the branch
        /// from which the <b>Neon.Kube</b> libraries were built.  In this case, this
        /// method will return a URI to the staged node image built from that branch.
        /// </para>
        /// <para>
        /// For non-release branches, this method will append a dot and the branch name
        /// to <see cref="KubeVersions.NeonKube"/> and include that in the URI.  This allows
        /// us to have multiple development versions of any given image for development and 
        /// testing purposes.  For release branches, the URI returned will reference the staged 
        /// node image including the <see cref="KubeVersions.NeonKube"/> without any branch part.
        /// </para>
        /// <note>
        /// Release branch names always start with: <b>"release-"</b>
        /// </note>
        /// </remarks>
        public static async Task<string> GetNodeImageUriAsync(
            HostingEnvironment  hostingEnvironment, 
            CpuArchitecture     architecture = CpuArchitecture.amd64,
            string              stageBranch  = null)
        {
            await SyncContext.Clear;

            return GetNodeImageUri(hostingEnvironment, architecture, stageBranch);

            using (var headendClient = HeadendClient.Create())
            {
                return await headendClient.ClusterSetup.GetNodeImageManifestUriAsync(hostingEnvironment.ToMemberString(), KubeVersions.NeonKube, architecture, stageBranch);
            }
        }

        /// <summary>
        /// Returns the URI of the download manifest for a neonKUBE desktop image.
        /// </summary>
        /// <param name="hostingEnvironment">Identifies the hosting environment.</param>
        /// <param name="architecture">Specifies the target CPU architecture.</param>
        /// <param name="stageBranch">
        /// To obtain the URI for a staged desktop image, pass this as the name of the
        /// branch from which neonKUBE libraries were built.
        /// </param>
        /// <returns>The action result.</returns>
        /// <remarks>
        /// <para>
        /// When <paramref name="stageBranch"/> is <c>null</c>, the URI for the published node
        /// image will be returned.
        /// </para>
        /// <para>
        /// Otherwise, <paramref name="stageBranch"/> should be passed as the name of the branch
        /// from which the <b>Neon.Kube</b> libraries were built.  In this case, this
        /// method will return a URI to the staged desktop image built from that branch.
        /// </para>
        /// <para>
        /// For non-release branches, this method will append a dot and the branch name
        /// to <see cref="KubeVersions.NeonKube"/> and include that in the URI.  This allows
        /// us to have multiple development versions of any given image for development and 
        /// testing purposes.  For release branches, the URI returned will reference the 
        /// staged desktop image including the <see cref="KubeVersions.NeonKube"/> without
        /// any branch part.
        /// </para>
        /// <note>
        /// Release branch names always start with: <b>"release-"</b>
        /// </note>
        /// </remarks>
        public static async Task<string> GetDesktopImageUriAsync(
            HostingEnvironment  hostingEnvironment,
            CpuArchitecture     architecture = CpuArchitecture.amd64,
            string              stageBranch  = null)
        {
            await SyncContext.Clear;

            return GetDesktopImageUri(hostingEnvironment, architecture, stageBranch);

            using (var headendClient = HeadendClient.Create())
            {
                return await headendClient.ClusterSetup.GetNodeImageManifestUriAsync(hostingEnvironment.ToMemberString(), KubeVersions.NeonKube, architecture, stageBranch);
            }
        }
    }
}
