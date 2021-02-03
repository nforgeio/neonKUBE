//-----------------------------------------------------------------------------
// FILE:	    V1CStorBlockDeviceRef.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:	Copyright (c) 2005-2021 by neonFORGE LLC.  All rights reserved.
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

using Newtonsoft.Json;
using Microsoft.Rest;

namespace Neon.Kube
{
    /// <summary>
    /// OpenEBS block device reference.
    /// </summary>
    public class V1CStorBlockDeviceRef
    {
        /// <summary>
        /// Initializes a new instance of the V1CStorBlockDeviceRef class.
        /// </summary>
        public V1CStorBlockDeviceRef()
        {
        }

        /// <summary>
        /// The name of the block device.
        /// </summary>
        [JsonProperty(PropertyName = "blockDeviceName", Required = Required.Always)]
        public string BlockDeviceName { get; set; }

        /// <summary>
        /// The capacity of block device.
        /// </summary>
        [JsonProperty(PropertyName = "capacity", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public BlockDeviceCapacity capacity { get; set; }

        /// <summary>
        /// The dev link for the block device.
        /// </summary>
        [JsonProperty(PropertyName = "devLink", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string DevLink { get; set; }
    }
}
