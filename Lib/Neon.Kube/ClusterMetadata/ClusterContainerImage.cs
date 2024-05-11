//-----------------------------------------------------------------------------
// FILE:        ClusterContainerImage.cs
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
    /// Holds information about a container image deployed as part of cluster setup.
    /// </summary>
    public class ClusterContainerImage
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public ClusterContainerImage()
        {
        }

        /// <summary>
        /// <para>
        /// Specifies the reference to the container image within one of the NEONFORGE
        /// container registeries.
        /// </para>
        /// <note>
        /// Source references have their tags set to the NeonKUBE cluster version.
        /// </note>
        /// </summary>
        [JsonProperty(PropertyName = "SourceRef", Required = Required.Always)]
        [YamlMember(Alias = "sourceRef", ApplyNamingConventions = false)]
        public string SourceRef { get; set; }

        /// <summary>
        /// Specifies the reference to the container image including the <b>image digest</b>
        /// within one of the NEONFORGE container registeries.
        /// </summary>
        [JsonProperty(PropertyName = "SourceDigestRef", Required = Required.Always)]
        [YamlMember(Alias = "sourceDigestRef", ApplyNamingConventions = false)]
        public string SourceDigestRef { get; set; }

        /// <summary>
        /// <para>
        /// Specifies the internal cluster reference to the container image as deployed
        /// within the cluster.  This is the reference used for persisting the container
        /// to the local registry as well as executing the container on cluster nodes
        /// via CRI-O.
        /// </para>
        /// <note>
        /// Internal references need to use the original tags because some related operators
        /// require that.  <b>neon-cluster-operator</b> uses this these references to download
        /// container images from <see cref="SourceRef"/> and then persist them to the local cluster
        /// registry as <see cref="InternalRef"/>.
        /// </note>
        /// </summary>
        [JsonProperty(PropertyName = "InternalRef", Required = Required.Always)]
        [YamlMember(Alias = "internalRef", ApplyNamingConventions = false)]
        public string InternalRef { get; set; }

        /// <summary>
        /// Specifies the reference to the container image including the <b>image digest</b>
        /// within as deployed within the cluster.  This is the reference used for persisting 
        /// the container to the local registry as well as executing the container on cluster
        /// nodes via CRI-O.
        /// </summary>
        [JsonProperty(PropertyName = "InternalDigestRef", Required = Required.Always)]
        [YamlMember(Alias = "internalDigestRef", ApplyNamingConventions = false)]
        public string InternalDigestRef { get; set; }
    }
}
