// -----------------------------------------------------------------------------
// FILE:	    IClusterSetupApi.cs
// CONTRIBUTOR: NEONFORGE Team
// COPYRIGHT:   Copyright © 2005-2023 by NEONFORGE LLC.  All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License").
// You may not use this file except in compliance with the License.
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
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Neon.Common;
using Neon.ModelGen;

namespace Neon.Kube.Models.Headend
{
    /// <summary>
    /// Defines the NEONCLOUD headend cluster setup REST API.
    /// </summary>
    [Target("all")]
    [Target("headend")]
    [ServiceModel(name: "Headend", group: "ClusterSetup")]
    [Route("cluster-setup")]
    [ApiVersion("2023-04-06")]
    public interface IClusterSetupApi
    {
        /// <summary>
        /// Generates a new DNS domain like <b>GUID.neoncloud.io</b> for a cluster and registers
        /// the required domain names with AWS Route53 using the cluster IP address passed.
        /// </summary>
        /// <returns>A dictionary returning the cluster <b>Id</b> and acceses <b>Token</b>.</returns>
        [HttpPost]
        [Route("create")]
        Dictionary<string, string> CreateClusterAsync();

        /// <summary>
        /// Returns the URI of the download manifest for an on-premise NEONKUBE node image.
        /// </summary>
        /// <param name="hostingEnvironment">Identifies the hosting environment.</param>
        /// <param name="version">Specifies the NEONKUBE version.</param>
        /// <param name="architecture">Specifies the target CPU architecture.</param>
        /// <param name="stageBranch">
        /// To obtain the URI for a specific staged node image, pass this as the name of the
        /// branch from which NEONKUBE libraries were built.  When <c>null</c> is passed, 
        /// the URI for the release image for the current build will be returned when the
        /// public release has been published, otherwise this will return the URI for the
        /// staged image.
        /// </param>
        /// <returns>The action result.</returns>
        [HttpGet]
        [Route("image/node")]
        string GetNodeImageManifestUriAsync(
            [FromQuery] string hostingEnvironment,
            [FromQuery] string version,
            [FromQuery] CpuArchitecture architecture,
            [FromQuery] string stageBranch);

        /// <summary>
        /// Returns the URI of the download manifest for a NEONKUBE desktop image.
        /// </summary>
        /// <param name="hostingEnvironment">Identifies the hosting environment.</param>
        /// <param name="version">Specifies the NEONKUBE version.</param>
        /// <param name="architecture">Specifies the target CPU architecture.</param>
        /// <param name="stageBranch">
        /// To obtain the URI for a specific staged node image, pass this as the name of the
        /// branch from which NEONKUBE libraries were built.  When <c>null</c> is passed, 
        /// the URI for the release image for the current build will be returned when the
        /// public release has been published, otherwise this will return the URI for the
        /// staged image.
        /// </param>
        /// <returns>The action result.</returns>
        [HttpGet]
        [Route("image/desktop")]
        string GetDesktopImageManifestUriAsync(
            [FromQuery] string hostingEnvironment,
            [FromQuery] string version,
            [FromQuery] CpuArchitecture architecture,
            [FromQuery] string stageBranch);

        /// <summary>
        /// Returns the Azure reference for a node image.
        /// </summary>
        /// <param name="version">Specifies the NEONKUBE version.</param>
        /// <param name="architecture">Specifies the target CPU architecture.</param>
        /// <param name="preview">Optionally specifies that returns details for the marketplace preview image.</param>
        /// <returns>The action result.</returns>
        [HttpGet]
        [Route("image/node/azure")]
        AzureImageDetails GetAzureImageDetailsAsync(
            [FromQuery] string version,
            [FromQuery] CpuArchitecture architecture,
            [FromQuery] bool preview = false);

        /// <summary>
        /// Ingests cluster setup log files.
        /// </summary>
        /// <param name="uploadId">UUID used to name the blob when persisted by the service.</param>
        /// <param name="timestampUtc">The timestamp (UTC) when the error occurred.</param>
        /// <param name="version">The NEONKUBE version.</param>
        /// <param name="clientId">The client installation UUID.</param>
        /// <param name="userId">The user ID or <see cref="Guid.Empty"/> before we implemented NEONCLOUD users.</param>
        /// <param name="preparing"><c>true</c> when the failure occured while preparing the cluster, <c>false</c> when setting it up.</param>
        /// <returns>The action result.</returns>
        [HttpPost]
        [BodyStream(IncludeContentSize = true)]
        [Route("deployment-log")]
        void PostDeploymentLogAsync(
            [FromQuery] string uploadId,
            [FromQuery] DateTime timestampUtc,
            [FromQuery] string version,
            [FromQuery] string clientId,
            [FromQuery] string userId,
            [FromQuery] bool preparing);
    }
}
