//-----------------------------------------------------------------------------
// FILE:	    ClusterLoginExport.cs
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
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using YamlDotNet.Serialization;

using Neon.Common;
using Neon.Cryptography;
using Neon.Kube;

namespace Neon.Kube.Config
{
    /// <summary>
    /// Holds all of the information required to import/export a cluster
    /// login.  This includes the Kubernetes cluster, login, and NEONKUBE
    /// extensions.
    /// </summary>
    public class ClusterLoginExport
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public ClusterLoginExport()
        {
        }

        /// <summary>
        /// The Kubernetes cluster.
        /// </summary>
        [JsonProperty(PropertyName = "cluster", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "cluster", ApplyNamingConventions = false)]
        public KubeConfigCluster Cluster { get; set; }

        /// <summary>
        /// The Kubernetes context.
        /// </summary>
        [JsonProperty(PropertyName = "context", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "context", ApplyNamingConventions = false)]
        public KubeConfigContext Context { get; set; }

        /// <summary>
        /// The Kubernetes user.
        /// </summary>
        [JsonProperty(PropertyName = "User", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "user", ApplyNamingConventions = false)]
        public KubeConfigUser User { get; set; }

        /// <summary>
        /// Ensures that the login information is valid.
        /// </summary>
        /// <exception cref="NeonKubeException">Thrown if the instance is invalid.</exception>
        public void Validate()
        {
            if (Context == null || User == null)
            {
                throw new NeonKubeException("Invalid login.");
            }
        }
    }
}
