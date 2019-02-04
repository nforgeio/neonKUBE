//-----------------------------------------------------------------------------
// FILE:	    NetConst.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.
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
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Retry;

namespace Neon.Net
{
    /// <summary>
    /// Network related constants.
    /// </summary>
    public class NetConst
    {
        /// <summary>
        /// <para>
        /// The default message transmission unit that is commonly configured
        /// across the internet.  This is the size in bytes of the largest
        /// packet including all of the protocol headers from OSI layers 3
        /// and above.  This does not include the data link (e.g. Ethernet)
        /// overhead.
        /// </para>
        /// <para>
        /// Packets larger than this may need to be fragmented (if allowed)
        /// to be transmitted end-to-end across several connected networks.
        /// </para>
        /// </summary>
        public const int DefaultMTU = 1500;

        /// <summary>
        /// The size of VXLAN headers in bytes.  <a href="VXLAN">https://en.wikipedia.org/wiki/Virtual_Extensible_LAN</a>
        /// is a protocol used in cloud and other virtualization environments
        /// to scale and separate network traffic between multiple tenants.
        /// Network traffic is empasulated in UDP packets with a header added
        /// to that identifies the virtual network.  This constant specifies
        /// the header overhead in bytes.
        /// </summary>
        public const int VXLANHeader = 8;

        /// <summary>
        /// The size in bytes of an IP packet header.
        /// </summary>
        public const int IPHeader = 20;

        /// <summary>
        /// The size in bytes of all headers (IP and TCP) added to a TCP packet.
        /// </summary>
        public const int TCPHeader = 20 + IPHeader;

        /// <summary>
        /// The size in bytes of an ICMP packet header.
        /// </summary>
        public const int ICMPHeader = 28;
    }
}
