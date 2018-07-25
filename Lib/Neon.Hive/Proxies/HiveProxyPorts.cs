//-----------------------------------------------------------------------------
// FILE:	    HiveProxyPorts.cs
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
    /// Describes the host ports and host port ranges that will be mapped into 
    /// hive <b>public</b> or <b>private</b> proxy containers.  Note that the
    /// host ports are mapped into the same local container port.
    /// </summary>
    public class HiveProxyPorts
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public HiveProxyPorts()
        {
        }

        /// <summary>
        /// Constructs an instance describing the ports and port ranges to be
        /// mapped into a hive proxy.
        /// </summary>
        /// <param name="range">The port range.</param>
        /// <param name="ports">The standalone p;orts (or <c>null</c>).</param>
        public HiveProxyPorts(HiveProxyPortRange range, IEnumerable<int> ports)
        {
            Covenant.Requires<ArgumentNullException>(range != null);

            if (!NetHelper.IsValidPort(range.FirstPort) ||
                !NetHelper.IsValidPort(range.LastPort) ||
                range.LastPort < range.FirstPort)
            {
                throw new ArgumentException($"Invalid hive proxy port range [{range.FirstPort}-{range.LastPort}].");
            }

            PortRange = range;

            if (ports != null)
            {
                foreach (var port in ports)
                {
                    if (!NetHelper.IsValidPort(port))
                    {
                        throw new ArgumentException($"Invalid hive proxy port [{port}].");
                    }

                    Ports.Add(port);
                }
            }
        }

        /// <summary>
        /// The port range to be mapped into a hive proxy.
        /// </summary>
        public HiveProxyPortRange PortRange { get; set; }

        /// <summary>
        /// The list of standalone ports to be mapped into a hive proxy.
        /// </summary>
        public List<int> Ports { get; set; } = new List<int>();

        /// <summary>
        /// Determines whether a port served by a hive proxy.
        /// </summary>
        /// <param name="port">The port being tested.</param>
        /// <returns><c>true</c> if the port is valid.</returns>
        public bool IsValidPort(int port)
        {
            if (Ports.Contains(port))
            {
                return true;
            }

            return PortRange.FirstPort <= port && port <= PortRange.LastPort;
        }

        /// <summary>
        /// Determines whether a port is valid proxy HTTP port for the proxy.
        /// </summary>
        /// <param name="port">The port being tested.</param>
        /// <returns><c>true</c> if the port is valid.</returns>
        public bool IsValidHttpPort(int port)
        {
            if (port == 80 && Ports.Contains(port))
            {
                return true;
            }

            // $hack(jeff.lill): Hardcoded to the first port in the range.

            return port == PortRange.FirstPort;
        }

        /// <summary>
        /// Determines whether a port is valid proxy HTTPS port for the proxy.
        /// </summary>
        /// <param name="port">The port being tested.</param>
        /// <returns><c>true</c> if the port is valid.</returns>
        public bool IsValidHttpsPort(int port)
        {
            if (port == 443 && Ports.Contains(port))
            {
                return true;
            }

            // $hack(jeff.lill): Hardcoded to the second port in the range.

            return port == PortRange.FirstPort + 1;
        }

        /// <summary>
        /// Determines whether a port is valid proxy TCP port for the proxy.
        /// </summary>
        /// <param name="port">The port being tested.</param>
        /// <returns><c>true</c> if the port is valid.</returns>
        public bool IsValidTcpPort(int port)
        {
            return PortRange.FirstPort <= port && port > PortRange.FirstPort + 1;
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            if (Ports.Count == 0)
            {
                return $"{PortRange.FirstPort}-{PortRange.LastPort}";
            }
            else
            {
                var sb = new StringBuilder();

                foreach (var port in Ports)
                {
                    sb.AppendWithSeparator(port.ToString(), "/");
                }

                sb.Append($" {PortRange.FirstPort}-{PortRange.LastPort}");

                return sb.ToString();
            }
        }
    }
}
