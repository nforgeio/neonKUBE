//-----------------------------------------------------------------------------
// FILE:	    ReachableHost.cs
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
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

using Neon.Common;

namespace Neon.Net
{
    /// <summary>
    /// Holds information about a reachable host returned by <see cref="NetHelper.GetReachableHost(IEnumerable{string}, ReachableHostMode)"/>.
    /// </summary>
    public class ReachableHost
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="host">The target hostname.</param>
        /// <param name="address">The target IP address or <c>null</c> if the target is un reachable.</param>
        /// <param name="time">The ping and answer round trip time.</param>
        /// <param name="unreachable">Optionally specifies that the host was unrechable.</param>
        public ReachableHost(string host, IPAddress address, TimeSpan time, bool unreachable = false)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(host));
            Covenant.Requires<ArgumentNullException>(address != null || unreachable);
            Covenant.Requires<ArgumentException>(time >= TimeSpan.Zero);

            this.Host        = host;
            this.Address     = address;
            this.Time        = time;
            this.Unreachable = unreachable;
        }

        /// <summary>
        /// Internal constructor used to create an instance from a <see cref="PingReply"/>.
        /// </summary>
        /// <param name="host">The target hostname.</param>
        /// <param name="pingReply">The ping reply.</param>
        /// <param name="unreachable">Optionally specifies that the host was unrechable.</param>
        internal ReachableHost(string host, PingReply pingReply, bool unreachable = false)
        {
            Covenant.Requires<ArgumentNullException>(pingReply != null);

            this.Host        = host;
            this.Address     = pingReply.Address;
            this.Time        = TimeSpan.FromMilliseconds(pingReply.RoundtripTime);
            this.Unreachable = unreachable;
        }

        /// <summary>
        /// The target host]name.
        /// </summary>
        public string Host { get; private set; }

        /// <summary>
        /// The target IP address or <c>null</c> if the target is unreachable.
        /// </summary>
        public IPAddress Address { get; private set; }

        /// <summary>
        /// The ping and answer round trip time.
        /// </summary>
        public TimeSpan Time { get; private set; }

        /// <summary>
        /// Indicates that the host was unreachable but was returned anyway because
        /// <see cref="ReachableHostMode.ReturnFirst"/> was specified.
        /// </summary>
        public bool Unreachable { get; private set; }
    }
}
