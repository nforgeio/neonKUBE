//-----------------------------------------------------------------------------
// FILE:	    V1CStorBlockDeviceSpec.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:	Copyright (c) 2005-2020 by neonFORGE LLC.  All rights reserved.
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
using System.Linq;
using System.Text;

using k8s;
using k8s.Models;

using Microsoft.Rest;

using Newtonsoft.Json;

namespace Neon.Kube
{
    /// <summary>
    /// The kubernetes spec for the block device.
    /// </summary>
    public partial class V1CStorBlockDeviceSpec
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        public V1CStorBlockDeviceSpec()
        {
        }

        /// <summary>
        /// The capacity of the block device.
        /// </summary>
        [JsonProperty(PropertyName = "capacity", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public BlockDeviceCapacity Capacity { get; set; }

        /// <summary>
        /// Details about the block device.
        /// </summary>
        [JsonProperty(PropertyName = "details", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public BlockDeviceDetails Details { get; set; }

        /// <summary>
        /// List of device links.
        /// </summary>
        [JsonProperty(PropertyName = "devLinks", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public List<string> DevLinks { get; set; }

        /// <summary>
        /// Filesystem information about the block device.
        /// </summary>
        [JsonProperty(PropertyName = "filesystem", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public FileSystemInfo FileSystem { get; set; }

        /// <summary>
        /// Attributes related to the node where the block device is mounted.
        /// </summary>
        [JsonProperty(PropertyName = "nodeAttributes", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public Dictionary<string, string> NodeAttributes { get; set; }

        /// <summary>
        /// Whether the block device is partitioned. (Yes/No)
        /// </summary>
        [JsonProperty(PropertyName = "partitioned", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string Partitioned { get; set; }

        /// <summary>
        /// The path.
        /// </summary>
        [JsonProperty(PropertyName = "path", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string Path { get; set; }
    }
}
