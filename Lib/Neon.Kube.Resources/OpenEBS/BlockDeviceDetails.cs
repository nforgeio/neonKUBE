//-----------------------------------------------------------------------------
// FILE:	    BlockDeviceDetails.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:	Copyright © 2005-2022 by NEONFORGE LLC.  All rights reserved.
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

namespace Neon.Kube.Resources
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
        [DefaultValue(null)]
        public string Compliance { get; set; }

        /// <summary>
        /// The device type.
        /// </summary>
        [DefaultValue(null)]
        [System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumMemberConverter))]
        public BlockDeviceType? DeviceType { get; set; }

        /// <summary>
        /// The drive type.
        /// </summary>
        [DefaultValue(null)]
        [System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumMemberConverter))]
        public DriveType? DriveType { get; set; }

        /// <summary>
        /// The Firmware revision.
        /// </summary>
        [DefaultValue(null)]
        public string FirmwareRevision { get; set; }

        /// <summary>
        /// The hardware sector size.
        /// </summary>
        [DefaultValue(null)]
        public long? HardwareSectorSize { get; set; }

        /// <summary>
        /// The logical block size.
        /// </summary>
        [DefaultValue(null)]
        public long? LogicalBlockSize { get; set; }

        /// <summary>
        /// The disk model.
        /// </summary>
        [DefaultValue(null)]
        public string Model { get; set; }

        /// <summary>
        /// The physical block size.
        /// </summary>
        [DefaultValue(null)]
        public long? PhysicalBlockSize { get; set; }

        /// <summary>
        /// The drive serial number.
        /// </summary>
        [DefaultValue(null)]
        public string Serial { get; set; }

        /// <summary>
        /// The drive vendor.
        /// </summary>
        [DefaultValue(null)]
        public string Vendor { get; set; }
    }
}
