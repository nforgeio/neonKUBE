//-----------------------------------------------------------------------------
// FILE:	    BlockDeviceDevLink.cs
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

namespace Neon.Kube
{
    /// <summary>
    /// OpenEBS block device links.
    /// </summary>
    public class BlockDeviceDevLink
    {
        /// <summary>
        /// Initializes a new instance of the BlockDeviceDevLink class.
        /// </summary>
        public BlockDeviceDevLink()
        {
        }

        /// <summary>
        /// The <see cref="DevLinkType"/>. Devices are listed by ID or by path.
        /// </summary>
        [DefaultValue(null)]
        public DevLinkType Kind { get; set; }

        /// <summary>
        /// List of device links. 
        /// </summary>
        [DefaultValue(null)]
        public List<string> Links { get; set; }
    }
}
