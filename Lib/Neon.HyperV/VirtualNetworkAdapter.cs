//-----------------------------------------------------------------------------
// FILE:	    VirtualNetworkAdapter.cs
// CONTRIBUTOR: Jeff Lill
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
using System.Diagnostics.Contracts;
using System.Dynamic;
using System.IO;
using System.Net;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Neon.Common;

namespace Neon.HyperV
{
    /// <summary>
    /// Describes a Hyper-V virtual network adapter attached to a virtual machine.
    /// </summary>
    public class VirtualNetworkAdapter
    {
        /// <summary>
        /// The adapter name.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// <c>true</c> if this adapter is attached to the management operating system.
        /// </summary>
        public bool IsManagementOs { get; set; }

        /// <summary>
        /// The name of the attached virtual machine.
        /// </summary>
        public string VMName { get; set; }

        /// <summary>
        /// The attached switch name.
        /// </summary>
        public string SwitchName { get; set; }

        /// <summary>
        /// The adapter's MAC address.
        /// </summary>
        public string MacAddress { get; set; }

        /// <summary>
        /// The adapter status.
        /// </summary>
        public string Status { get; set; }

        /// <summary>
        /// The IP addresses assigned to the adapter.
        /// </summary>
        public List<IPAddress> Addresses { get; set; } = new List<IPAddress>();
    }
}
