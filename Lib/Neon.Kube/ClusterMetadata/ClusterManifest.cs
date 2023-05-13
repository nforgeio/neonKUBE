//-----------------------------------------------------------------------------
// FILE:	    ClusterManifest.cs
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
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft;
using Newtonsoft.Json;
using YamlDotNet.Serialization;

using Neon.Common;
using Neon.Kube;
using Neon.IO;
using Neon.Net;
using Neon.SSH;

namespace Neon.Kube
{
    /// <summary>
    /// <para>
    /// Holds information about a deployed cluster including things like the container images
    /// that need to be present in the local Harbor deployment.  This information is associated
    /// with a specific version of NEONKUBE and is generated automatically during NEONCLOUD
    /// node image builds and is uploaded to S3 as a JSON document.
    /// </para>
    /// <para>
    /// This ends up being embedded into the <b>neon-cluster-operator</b> as a resource via
    /// a build task that uses the <b>neon-build get-cluster-manifest</b> command to download the
    /// file from S3 so it can be included in the project.
    /// </para>
    /// </summary>
    public class ClusterManifest
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public ClusterManifest()
        {
        }

        /// <summary>
        /// Returns information about the container images deployed to a new NEONKUBE cluster.
        /// </summary>
        [JsonProperty(PropertyName = "ContainerImages", Required = Required.Always)]
        [YamlMember(Alias = "containerImages", ApplyNamingConventions = false)]
        public List<ClusterContainerImage> ContainerImages { get; set; } = new List<ClusterContainerImage>();
    }
}
