//-----------------------------------------------------------------------------
// FILE:	    HeadendClient.cs
// CONTRIBUTOR: Marcus Bowyer
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
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Neon.Common;
using Neon.ModelGen;

namespace Neon.Kube.Models.Headend
{
    [Target("all")]
    [Target("headend")]
    [ServiceModel(name: "Headend", group: "ClusterSetup")]
    [Route("cluster-setup")]
    [ApiVersion("0.2")]
    public interface IClusterSetupController
    {
        [HttpPost]
        [Route("create")]
        Dictionary<string, string> CreateClusterAsync();

        [HttpGet]
        [Route("image/node")]
        string GetNodeImageManifestUriAsync(
            [FromQuery] string hostingEnvironment,
            [FromQuery] string version,
            [FromQuery] CpuArchitecture architecture,
            [FromQuery] bool useStaged);

        [HttpGet]
        [Route("image/desktop")]
        string GetDesktopImageManifestUriAsync(
            [FromQuery] string hostingEnvironment,
            [FromQuery] string version,
            [FromQuery] CpuArchitecture architecture,
            [FromQuery] bool useStaged);

        [HttpGet]
        [Route("image/node/azure")]
        AzureImageDetails GetAzureImageDetailsAsync(
            [FromQuery] string version,
            [FromQuery] CpuArchitecture architecture);

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

    /// <summary>
    /// Implements cluster methods.
    /// </summary>
    [Target("all")]
    [Target("headend")]
    [ServiceModel(name: "Headend", group: "Cluster")]
    [Route("cluster")]
    [ApiVersion("0.2")]
    public interface IClusterController
    {
        [HttpPut]
        [Route("{clusterId}/domain")]
        string UpdateClusterDomainAsync(
            [FromRoute] string clusterId,
            [FromQuery] string addresses);

        [HttpPost]
        [Route("{clusterId}/sso-client")]
        Dictionary<string, string> CreateSsoClientAsync(
            [FromRoute] string clusterId,
            [FromQuery] string clusterName);

        [HttpGet]
        [Route("{clusterId}/token/renew")]
        string GetTokenAsync([FromRoute] string clusterId);
    }

    /// <summary>
    /// Implements cluster methods.
    /// </summary>
    [Target("all")]
    [Target("headend")]
    [ServiceModel(name: "Headend", group: "NeonDesktop")]
    [Route("")]
    [ApiVersion("0.2")]
    public interface INeonDesktopController
    {
        [HttpGet]
        [Route("neondesktop/certificate")]
        IDictionary<string, byte[]> GetNeonDesktopCertificateAsync();
    }
}
