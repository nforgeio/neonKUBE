//-----------------------------------------------------------------------------
// FILE:	    HostAddresses.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Consul;
using ICSharpCode.SharpZipLib.Zip;
using Newtonsoft.Json;

using Neon.Common;
using Neon.Cryptography;
using Neon.Diagnostics;
using Neon.Docker;
using Neon.Hive;
using Neon.Net;

namespace NeonDnsMon
{
    /// <summary>
    /// Maps hostnames to one or more IP addresses.
    /// </summary>
    public class HostAddresses : Dictionary<string, List<IPAddress>>
    {
        /// <summary>
        /// Adds a hostname to IP address mapping.
        /// </summary>
        /// <param name="hostname">The hostname.</param>
        /// <param name="address">The associated IP address.</param>
        /// <remarks>
        /// <note>
        /// This method is threadsafe against other calls to <see cref="Add(string, IPAddress)"/>
        /// so that hosts can be health checked and added on multiple threads.
        /// </note>
        /// </remarks>
        public void Add(string hostname, IPAddress address)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(hostname));

            hostname = hostname.ToLowerInvariant();

            lock (this)
            {
                if (!TryGetValue(hostname, out var addressList))
                {
                    addressList = new List<IPAddress>();
                    Add(hostname, addressList);
                }

                addressList.Add(address);
            }
        }
    }
}
