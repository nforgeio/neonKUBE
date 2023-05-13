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
        /// The GitHub organization hosting NEONKUBE releases container images.
        /// </summary>
        public const string NeonKubeReleasePackageOrg = "neonkube-release";

        /// <summary>
        /// The GitHub organization hosting NEONKUBE staged container images.
        /// </summary>
        public const string NeonKubeStagePackageOrg = "neonkube-stage";

        /// <summary>
        /// The name of the AWS bucket used for published NEONKUBE releases.
        /// </summary>
        public const string NeonKubeReleaseBucketName = "neonkube-release";

        /// <summary>
        /// The URI for the public AWS S3 bucket for public NEONKUBE releases
        /// </summary>
        public const string NeonKubeReleaseBucketUri = $"https://{NeonKubeReleaseBucketName}.s3.us-west-2.amazonaws.com";

        /// <summary>
        /// The name of the AWS bucket used for staged NEONKUBE releases.
        /// </summary>
        public const string NeonKubeStageBucketName = "neonkube-stage";

        /// <summary>
        /// The URI for the public AWS S3 bucket for public NEONKUBE releases
        /// </summary>
        public const string NeonKubeStageBucketUri = $"https://{NeonKubeStageBucketName}.s3.us-west-2.amazonaws.com";

        /// <summary>
        /// The URI for the cluster manifest (<see cref="ClusterManifest"/>) JSON file for the current
        /// NEONKUBE cluster version.
        /// </summary>
        public static readonly string NeonClusterManifestUri = $"{NeonKubeStageBucketUri}/manifests/neonkube-{KubeVersions.NeonKubeWithBranchPart}.json";

        /// <summary>
        /// Returns the URI of the download manifest for a NEONKUBE node image.
        /// </summary>
        /// <param name="hostingEnvironment">Identifies the hosting environment.</param>
        /// <param name="architecture">Specifies the target CPU architecture.</param>
        /// <param name="stageBranch">
        /// To obtain the URI for a specific staged node image, pass this as the name of the
        /// branch from which NEONKUBE libraries were built.  When <c>null</c> is passed, 
        /// the URI for the release image for the current build will be returned when the
        /// public release has been published, otherwise this will return the URI for the
        /// staged image.
        /// </param>
        /// <returns>The action result.</returns>
        public static async Task<string> GetNodeImageUriAsync(
            HostingEnvironment  hostingEnvironment, 
            CpuArchitecture     architecture = CpuArchitecture.amd64,
            string              stageBranch  = null)
        {
            await SyncContext.Clear;

            using (var headendClient = HeadendClient.Create())
            {
                return await headendClient.ClusterSetup.GetNodeImageManifestUriAsync(hostingEnvironment.ToMemberString(), KubeVersions.NeonKube, architecture, stageBranch);
            }
        }

        /// <summary>
        /// Returns the URI of the download manifest for a NEONKUBE desktop image.
        /// </summary>
        /// <param name="hostingEnvironment">Identifies the hosting environment.</param>
        /// <param name="architecture">Specifies the target CPU architecture.</param>
        /// <param name="stageBranch">
        /// To obtain the URI for a specific staged node image, pass this as the name of the
        /// branch from which NEONKUBE libraries were built.  When <c>null</c> is passed, 
        /// the URI for the release image for the current build will be returned when they
        /// public release has been published, otherwise this will return the URI for the
        /// staged image.
        /// </param>
        /// <returns>The action result.</returns>
        public static async Task<string> GetDesktopImageUriAsync(
            HostingEnvironment  hostingEnvironment,
            CpuArchitecture     architecture = CpuArchitecture.amd64,
            string              stageBranch  = null)
        {
            await SyncContext.Clear;

            using (var headendClient = HeadendClient.Create())
            {
                return await headendClient.ClusterSetup.GetDesktopImageManifestUriAsync(hostingEnvironment.ToMemberString(), KubeVersions.NeonKube, architecture, stageBranch);
            }
        }
    }
}
