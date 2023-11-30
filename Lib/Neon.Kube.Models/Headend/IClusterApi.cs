// -----------------------------------------------------------------------------
// FILE:	    IClusterApi.cs
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
    /// Defines the headend cluster management REST APIs.
    /// </summary>
    [Target("all")]
    [Target("headend")]
    [ServiceModel(name: "Headend", group: "Cluster")]
    [Route("cluster")]
    [ApiVersion("2023-04-06")]
    public interface IClusterApi
    {
        /// <summary>
        /// Updates a cluster's DNS records to reference a new IP address.
        /// </summary>
        /// <param name="clusterId">The cluster ID.</param>
        /// <param name="addresses">The new IP addresses.</param>
        /// <returns>The action result.</returns>
        [HttpPut]
        [Route("{clusterId}/domain")]
        string UpdateClusterDomainAsync(
            [FromRoute] string clusterId,
            [FromQuery] string addresses);

        /// <summary>
        /// Creates an SSO client for a cluster.
        /// </summary>
        /// <param name="clusterId">The cluster ID.</param>
        /// <param name="clusterName"></param>
        /// <returns>A dictionary holding the <b>ClientId</b>, <b>Secret</b>, and <b>RedirectURI</b>.</returns>
        [HttpPost]
        [Route("{clusterId}/sso-client")]
        Dictionary<string, string> CreateSsoClientAsync(
            [FromRoute] string clusterId,
            [FromQuery] string clusterName);

        /// <summary>
        /// Renews a cluster JWT.
        /// </summary>
        /// <param name="clusterId"></param>
        /// <returns>The updated JWT.</returns>
        [HttpGet]
        [Route("{clusterId}/token/renew")]
        string GetTokenAsync([FromRoute] string clusterId);

        /// <summary>
        /// Returns a cluster's certificate information.
        /// </summary>
        /// <param name="clusterId">Specifies the cluster ID.</param>
        /// <returns>A dictionary with the certificate <b>tls.crt</b> and private <b>tls.key</b>.</returns>
        [HttpGet]
        [Route("{clusterId}/certificate")]
        IDictionary<string, byte[]> GetCertificateAsync([FromRoute] string clusterId);
    }
}
