//-----------------------------------------------------------------------------
// FILE:	    AcmeController.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:   Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.
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
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

using Neon.Common;
using Neon.Cryptography;
using Neon.Diagnostics;
using Neon.Service;
using Neon.Kube;
using Neon.Tasks;
using Neon.Net;
using Neon.Web;

using k8s;
using k8s.Models;
using System.Text.RegularExpressions;

namespace NeonAcme.Controllers
{
/// <summary>
/// Implements neon-acme service methods.
/// </summary>
    [ApiController]
    [Route("apis/acme.neoncloud.io/v1alpha1")]
    public class AcmeController : NeonControllerBase
    {
        private Service             service;
        private JsonClient          jsonClient;
        private KubernetesWithRetry k8s;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="jsonClient">The JSON client for interacting with the headend.</param>
        public AcmeController(
            Service    service,
            JsonClient jsonClient)
        {
            this.service    = service;
            this.jsonClient = jsonClient;
            this.k8s        = service.Kubernetes;
        }

        /// <summary>
        /// <para>
        /// This method is used by Kubernetes for service discovery.
        /// </para>
        /// </summary>
        /// <returns>The <see cref="V1APIResourceList"/> detailing the resources available.</returns>
        [HttpGet("")]
        [Produces("application/json")]
        public async Task<ActionResult> DiscoveryAsync()
        {
            await SyncContext.Clear;

            Logger.LogDebugEx(() => $"Headers: {NeonHelper.JsonSerialize(HttpContext.Request.Headers)}");

            return new JsonResult(service.Resources);
        }

        /// <summary>
        /// Handles challenge presentations from Cert Manager.
        /// </summary>
        /// <param name="challenge"></param>
        /// <returns></returns>
        [HttpPost("neoncluster_io")]
        [Produces("application/json")]
        public async Task<ActionResult> PresentNeonclusterChallengeAsync([FromBody] ChallengePayload challenge)
        {
            Logger.LogInformationEx(() => $"Challenge request [{challenge.Request.Action}] [{challenge.Request.DnsName}]");
            Logger.LogDebugEx(() => $"Headers: {NeonHelper.JsonSerialize(HttpContext.Request.Headers)}");
            Logger.LogDebugEx(NeonHelper.JsonSerialize(challenge));

            var response = await jsonClient.PostAsync<ChallengePayload>("acme/challenge", challenge);

            challenge.Response = response.Response;

            return new JsonResult(challenge);
        }
    }
}