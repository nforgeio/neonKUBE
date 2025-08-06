//-----------------------------------------------------------------------------
// FILE:        KubeDownloads.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright © 2005-2024 by NEONFORGE LLC.  All rights reserved.
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
        public static readonly string HelmLinuxUri = $"https://get.helm.sh/helm-v{KubeVersion.Helm}-linux-amd64.tar.gz";

        /// <summary>
        /// The Helm binary URL for OS/X.
        /// </summary>
        public static readonly string HelmOsxUri = $"https://get.helm.sh/helm-v{KubeVersion.Helm}-darwin-amd64.tar.gz";

        /// <summary>
        /// The Helm binary URL for Windows.
        /// </summary>
        public static readonly string HelmWindowsUri = $"https://get.helm.sh/helm-v{KubeVersion.Helm}-windows-amd64.zip";

        /// <summary>
        /// The GitHub organization hosting NeonKUBE releases container images.
        /// </summary>
        public const string NeonKubeReleasePackageOrg = "neonkube-release";

        /// <summary>
        /// The GitHub organization hosting NeonKUBE staged container images.
        /// </summary>
        public const string NeonKubeStagePackageOrg = "neonkube-stage";

        /// <summary>
        /// The name of the AWS bucket used for published NeonKUBE releases.
        /// </summary>
        public const string NeonKubeReleaseBucketName = "neonkube-release";

        /// <summary>
        /// The URI for the public AWS S3 bucket for public NeonKUBE releases
        /// </summary>
        public const string NeonKubeReleaseBucketUri = $"https://{NeonKubeReleaseBucketName}.s3.us-west-2.amazonaws.com";

        /// <summary>
        /// The name of the AWS bucket used for staged NeonKUBE releases.
        /// </summary>
        public const string NeonKubeStageBucketName = "neonkube-stage";

        /// <summary>
        /// The URI for the public AWS S3 bucket for public NeonKUBE releases
        /// </summary>
        public const string NeonKubeStageBucketUri = $"https://{NeonKubeStageBucketName}.s3.us-west-2.amazonaws.com";

        /// <summary>
        /// Specifies the S3 key for the cluster manifest within the <see cref="NeonKubeStageBucketName"/> bucket.
        /// </summary>
        public static readonly string ClusterManifestKey = $"manifests/neonkube-{KubeVersion.NeonKubeWithBranchPart}.json";

        /// <summary>
        /// The URI for the cluster manifest (<see cref="ClusterManifest"/>) JSON file for the current
        /// NeonKUBE cluster version.
        /// </summary>
        public static readonly string NeonClusterManifestUri = $"{NeonKubeStageBucketUri}/{ClusterManifestKey}";

        /// <summary>
        /// Returns the URI of the download for NEONUBE base images.
        /// </summary>
        /// <param name="hostingEnvironment">Identifies the hosting environment.</param>
        /// <param name="architecture">Specifies the target CPU architecture.</param>
        /// <returns>The image URI.</returns>
        public static async Task<string> GetBaseImageUri(
            HostingEnvironment  hostingEnvironment,
            CpuArchitecture     architecture = CpuArchitecture.amd64)
        {
            await SyncContext.Clear;

            // $todo(jefflill):
            //
            // This is hardcoded to our S3 bucket for now.  We should probably
            // change this to hit the headend as well.

            switch (hostingEnvironment)
            {
                case HostingEnvironment.HyperV:

                    return $"https://neonkube-stage.s3.us-west-2.amazonaws.com/images/hyperv/base/ubuntu-22.04.hyperv.{architecture}.vhdx.gz.manifest";

                case HostingEnvironment.XenServer:

                    return $"https://neonkube-stage.s3.us-west-2.amazonaws.com/images/xenserver/base/ubuntu-22.04.xenserver.{architecture}.xva.gz.manifest";

                default:

                    throw new ArgumentException($"Hosting environment [{hostingEnvironment}] does not support base image downloads.", nameof(hostingEnvironment));
            }
        }

        /// <summary>D
        /// Returns the URI of the download manifest for a NeonKUBE node image.
        /// </summary>
        /// <param name="hostingEnvironment">Identifies the hosting environment.</param>
        /// <param name="architecture">Specifies the target CPU architecture.</param>
        /// <param name="stageBranch">
        /// To obtain the URI for a specific staged node image, pass this as the name of the
        /// branch from which NeonKUBE libraries were built.  When <c>null</c> is passed, 
        /// the URI for the release image for the current build will be returned when the
        /// public release has been published, otherwise this will return the URI for the
        /// staged image.
        /// </param>
        /// <returns>The image URI.</returns>
        public static async Task<string> GetNodeImageUriAsync(
            HostingEnvironment  hostingEnvironment, 
            CpuArchitecture     architecture = CpuArchitecture.amd64,
            string              stageBranch  = null)
        {
            await SyncContext.Clear;

            using (var headendClient = HeadendClient.Create())
            {
                return await headendClient.ClusterSetup.GetNodeImageManifestUriAsync(hostingEnvironment.ToMemberString(), KubeVersion.NeonKube, architecture, stageBranch);
            }
        }

        /// <summary>
        /// Returns the URI of the download manifest for a NeonKUBE desktop image.
        /// </summary>
        /// <param name="hostingEnvironment">Identifies the hosting environment.</param>
        /// <param name="architecture">Specifies the target CPU architecture.</param>
        /// <param name="stageBranch">
        /// To obtain the URI for a specific staged node image, pass this as the name of the
        /// branch from which NeonKUBE libraries were built.  When <c>null</c> is passed, 
        /// the URI for the release image for the current build will be returned when they
        /// public release has been published, otherwise this will return the URI for the
        /// staged image.
        /// </param>
        /// <returns>The image URI.</returns>
        public static async Task<string> GetDesktopImageUriAsync(
            HostingEnvironment  hostingEnvironment,
            CpuArchitecture     architecture = CpuArchitecture.amd64,
            string              stageBranch  = null)
        {
            await SyncContext.Clear;

            using (var headendClient = HeadendClient.Create())
            {
                return await headendClient.ClusterSetup.GetDesktopImageManifestUriAsync(hostingEnvironment.ToMemberString(), KubeVersion.NeonKube, architecture, stageBranch);
            }
        }
    }
}
