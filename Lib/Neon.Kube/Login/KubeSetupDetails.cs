//-----------------------------------------------------------------------------
// FILE:	    KubeSetupDetails.cs
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
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using YamlDotNet.Core;
using YamlDotNet.Serialization;

using Neon.Common;
using Neon.IO;

namespace Neon.Kube
{
    /// <summary>
    /// Holds details required during setup or for provisioning 
    /// additional cluster nodes.
    /// </summary>
    public class KubeSetupDetails
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public KubeSetupDetails()
        {
        }

        /// <summary>
        /// Indicates whether provisioning is complete but setup is still
        /// pending for this cluster
        /// </summary>
        [JsonProperty(PropertyName = "SetupPending", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "setupPending", ApplyNamingConventions = false)]
        [DefaultValue(false)]
        public bool SetupPending { get; set; } = false;

        /// <summary>
        /// The command to be used join nodes to an existing cluster.
        /// </summary>
        [JsonProperty(PropertyName = "ClusterJoinCommand", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "clusterJoinCommand", ScalarStyle = ScalarStyle.Literal, ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string ClusterJoinCommand { get; set; }

        /// <summary>
        /// Holds files captured from the boot control-plane node that will need to be provisioned
        /// on the remaining control-plane nodes.  The dictionary key is the file path and the value 
        /// specifies the file text, permissions, and owner.
        /// </summary>
        [JsonProperty(PropertyName = "ControlNodeFiles", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "controlNodeFiles", ScalarStyle = ScalarStyle.Literal, ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public Dictionary<string, KubeFileDetails> ControlNodeFiles { get; set; } = new Dictionary<string, KubeFileDetails>();
    }
}
