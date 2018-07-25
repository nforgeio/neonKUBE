//-----------------------------------------------------------------------------
// FILE:	    HiveProxyPortRange.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Consul;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Neon.Common;
using Neon.Docker;
using Neon.IO;
using Neon.Net;
using Neon.Retry;
using Neon.Time;

namespace Neon.Hive
{
    /// <summary>
    /// Describes a range of ports to be that will be mapped into 
    /// hive <b>public</b> or <b>private</b> proxy containers.
    /// </summary>
    public class HiveProxyPortRange
    {
        /// <summary>
        /// Constructs a range of mapped proxy ports.
        /// </summary>
        /// <param name="firstPort">The first port in the range.</param>
        /// <param name="lastPort">The last port in the range.</param>
        public HiveProxyPortRange(int firstPort, int lastPort)
        {
            Covenant.Requires<ArgumentException>(0 < firstPort && firstPort <= ushort.MaxValue);
            Covenant.Requires<ArgumentException>(0 < lastPort && lastPort <= ushort.MaxValue);
            Covenant.Requires<ArgumentException>(firstPort <= lastPort);

            this.FirstPort = firstPort;
            this.LastPort  = lastPort;
        }

        /// <summary>
        /// The first port in the range.
        /// </summary>
        public int FirstPort { get; set; }

        /// <summary>
        /// The last port in the range.
        /// </summary>
        public int LastPort { get; set; }
    }
}
