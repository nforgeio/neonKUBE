//-----------------------------------------------------------------------------
// FILE:	    V1CStorBlockDeviceSpec.cs
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

namespace Neon.Kube.Resources.OpenEBS
{
    /// <summary>
    /// The kubernetes spec for the block device.
    /// </summary>
    public class V1CStorBlockDeviceSpec
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
        [DefaultValue(null)]
        public BlockDeviceCapacity Capacity { get; set; }

        /// <summary>
        /// Details about the block device.
        /// </summary>
        [DefaultValue(null)]
        public BlockDeviceDetails Details { get; set; }

        /// <summary>
        /// List of device links.
        /// </summary>
        [DefaultValue(null)]
        public List<BlockDeviceDevLink> DevLinks { get; set; }

        /// <summary>
        /// Filesystem information about the block device.
        /// </summary>
        [DefaultValue(null)]
        public FileSystemInfo FileSystem { get; set; }

        /// <summary>
        /// Attributes related to the node where the block device is mounted.
        /// </summary>
        [DefaultValue(null)]
        public Dictionary<string, string> NodeAttributes { get; set; }

        /// <summary>
        /// Whether the block device is partitioned. (Yes/No)
        /// </summary>
        [DefaultValue(null)]
        public string Partitioned { get; set; }

        /// <summary>
        /// The path.
        /// </summary>
        [DefaultValue(null)]
        public string Path { get; set; }
    }
}
