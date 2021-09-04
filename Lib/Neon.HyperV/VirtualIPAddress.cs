//-----------------------------------------------------------------------------
// FILE:	    VirtualMachine.cs
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
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Net;

namespace Neon.HyperV
{
    /// <summary>
    /// Describes a IP address.
    /// </summary>
    public class VirtualIPAddress
    {
        /// <summary>
        /// The associated IP address.
        /// </summary>
        public string Address { get; set; }

        /// <summary>
        /// The IP address subnet.
        /// </summary>
        public NetworkCidr Subnet { get; set; }

        /// <summary>
        /// Identifies the network interface or switch to which this address
        /// is connected.
        /// </summary>
        public string InterfaceName { get; set;}
    }
}
