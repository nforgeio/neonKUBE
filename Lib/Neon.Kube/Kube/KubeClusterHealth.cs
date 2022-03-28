//-----------------------------------------------------------------------------
// FILE:	    KubeClusterHealth.cs
// CONTRIBUTOR: Jeff Lill
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
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Dynamic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Rest;
using Microsoft.Win32;
using SharpCompress.Readers;

using Newtonsoft.Json;

using Neon.Common;

namespace Neon.Kube
{
    /// <summary>
    /// Describes the current health of a cluster.
    /// </summary>
    public class KubeClusterHealth
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public KubeClusterHealth()
        {
        }

        /// <summary>
        /// The neonKUBE version of the cluster or <b>"0"</b> when the version
        /// is not known.  This is formatted as a <see cref="SemanticVersion"/>.
        /// </summary>
        [JsonProperty(PropertyName = "Version", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(KubeVersions.NeonKube)]
        public string Version { get; set; } = KubeVersions.NeonKube;

        /// <summary>
        /// The cluster health state.
        /// </summary>
        [JsonProperty(PropertyName = "State", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(KubeClusterState.Unknown)]
        public KubeClusterState State { get; set; }

        /// <summary>
        /// Human readable text summarizing the cluster health state.
        /// </summary>
        [JsonProperty(PropertyName = "Summary", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string Summary { get; set; }

        /// <summary>
        /// Describes which optional components have been deployed to the cluster.
        /// </summary>
        [JsonProperty(PropertyName = "OptionalComponents", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public ClusterOptionalComponents OptionalComponents { get; set; } = new ClusterOptionalComponents();
    }
}
