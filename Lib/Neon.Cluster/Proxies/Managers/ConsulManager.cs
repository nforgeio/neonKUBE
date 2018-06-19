//-----------------------------------------------------------------------------
// FILE:	    ConsulManager.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
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

namespace Neon.Cluster
{
    /// <summary>
    /// Handles HashiCorp Consul related operations for a <see cref="ClusterProxy"/>.
    /// </summary>
    public sealed class ConsulManager : IDisposable
    {
        private object          syncRoot = new object();
        private ClusterProxy    cluster;
        private ConsulClient    client;

        /// <summary>
        /// Internal constructor.
        /// </summary>
        /// <param name="cluster">The parent <see cref="ClusterProxy"/>.</param>
        internal ConsulManager(ClusterProxy cluster)
        {
            Covenant.Requires<ArgumentNullException>(cluster != null);

            this.cluster = cluster;
        }

        /// <summary>
        /// Releases all resources associated with the instance.
        /// </summary>
        public void Dispose()
        {
            lock (syncRoot)
            {
                if (client != null)
                {
                    try
                    {
                        client.Dispose();
                    }
                    catch (ObjectDisposedException)
                    {
                        // Intentially ignoring these.
                    }
                    finally
                    {
                        client = null;
                    }
                }
            }

            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Returns a Consul client.
        /// </summary>
        /// <returns>The <see cref="ConsulClient"/>.</returns>
        public ConsulClient Client
        {
            get
            {
                lock (syncRoot)
                {
                    if (client != null)
                    {
                        return client;
                    }

                    client = NeonClusterHelper.OpenConsul();
                }

                return client;
            }
        }
    }
}
