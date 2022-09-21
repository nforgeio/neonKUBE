﻿//-----------------------------------------------------------------------------
// FILE:	    HeadendClient.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:	Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.
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

using Neon.Kube;
using Neon.ModelGen;

namespace Neon.Kube.Models
{
    [Target("all")]
    [Target("headend")]
    [ServiceModel(name: "Headend", group: "ClusterSetup")]
    [Route("cluster-setup")]
    [ApiVersion("0.1")]
    public interface IClusterSetupController
    {
        [HttpPost]
        [Route("create")]
        Dictionary<string, string> CreateClusterAsync(
            [FromQuery] string addresses, 
            [FromQuery] string ipAddress);

        [HttpGet]
        [Route("image/download")]
        string GetNodeImageManifestUriAsync(
            [FromQuery] string hostingEnvironment,
            [FromQuery] string version,
            [FromQuery] string architecture);

        [HttpGet]
        [Route("image/azure")]
        AzureImageReference GetAzureImageReferenceAsync(
            [FromQuery] string hostingEnvironment,
            [FromQuery] string version,
            [FromQuery] string architecture);

        [HttpPost]
        [BodyStream]
        [Route("logs")]
        AzureImageReference UploadClusterSetupLogAsync(
            [FromQuery] string clientId,
            [FromQuery] string userId,
            [FromQuery] string details = null);
    }

    /// <summary>
    /// Implements cluster methods.
    /// </summary>
    [Target("all")]
    [Target("headend")]
    [ServiceModel(name: "Headend", group: "Cluster")]
    [Route("cluster")]
    [ApiVersion("0.1")]
    public interface IClusterController
    {

    }

    /// <summary>
    /// Implements ACME methods.
    /// </summary>
    [Target("all")]
    [Target("headend")]
    [ServiceModel(name: "Headend", group: "Acme")]
    [Route("acme")]
    [ApiVersion("0.1")]
    public interface IAcmeController
    {

    }
}