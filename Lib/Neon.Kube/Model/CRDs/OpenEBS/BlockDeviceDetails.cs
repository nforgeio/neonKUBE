//-----------------------------------------------------------------------------
// FILE:	    BlockDeviceDetails.cs
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
    /// OpenEBS block device details.
    /// </summary>
    public class BlockDeviceDetails
    {
        /// <summary>
        /// Initializes a new instance of the BlockDeviceDetails class.
        /// </summary>
        public BlockDeviceDetails()
        {
        }

        /// <summary>
        /// 
        /// </summary>
        [JsonProperty(PropertyName = "compliance", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string Compliance { get; set; }

        /// <summary>
        /// The device type.
        /// </summary>
        [JsonProperty(PropertyName = "deviceType", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public BlockDeviceType? DeviceType { get; set; }

        /// <summary>
        /// The drive type.
        /// </summary>
        [JsonProperty(PropertyName = "driveType", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public DriveType? DriveType { get; set; }

        /// <summary>
        /// The Firmware revision.
        /// </summary>
        [JsonProperty(PropertyName = "firmwareRevision", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string FirmwareRevision { get; set; }

        /// <summary>
        /// The hardware sector size.
        /// </summary>
        [JsonProperty(PropertyName = "hardwareSectorSize", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public long? HardwareSectorSize { get; set; }

        /// <summary>
        /// The logical block size.
        /// </summary>
        [JsonProperty(PropertyName = "logicalBlockSize", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public long? LogicalBlockSize { get; set; }

        /// <summary>
        /// The disk model.
        /// </summary>
        [JsonProperty(PropertyName = "model", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string Model { get; set; }

        /// <summary>
        /// The physical block size.
        /// </summary>
        [JsonProperty(PropertyName = "physicalBlockSize", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public long? PhysicalBlockSize { get; set; }

        /// <summary>
        /// The drive serial number.
        /// </summary>
        [JsonProperty(PropertyName = "serial", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string Serial { get; set; }

        /// <summary>
        /// The drive vendor.
        /// </summary>
        [JsonProperty(PropertyName = "vendor", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string Vendor { get; set; }
    }
}
