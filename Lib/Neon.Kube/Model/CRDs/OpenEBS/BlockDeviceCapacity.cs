//-----------------------------------------------------------------------------
// FILE:	    BlockDeviceCapacity.cs
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
using System.Numerics;
using System.Text;

using k8s;
using k8s.Models;

namespace Neon.Kube
{
    /// <summary>
    /// Describes an OpenEBS block device capacity.
    /// </summary>
    public class BlockDeviceCapacity
    {
        /// <summary>
        /// Initializes a new instance of the BlockDeviceCapacity class.
        /// </summary>
        public BlockDeviceCapacity()
        {
        }

        /// <summary>
        /// The logical sector size.
        /// </summary>
        [DefaultValue(null)]
        public long? LogicalSectorSize { get; set; }

        /// <summary>
        /// The physical sector size.
        /// </summary>
        [DefaultValue(null)]
        public long? PhysicalSectorSize { get; set; }

        /// <summary>
        /// The storage size.
        /// </summary>
        [DefaultValue(null)]
        public long? Storage { get; set; }
    }
}
