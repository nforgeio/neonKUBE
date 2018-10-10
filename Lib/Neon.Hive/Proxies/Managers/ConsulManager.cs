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

namespace Neon.Hive
{
    /// <summary>
    /// Handles HashiCorp Consul related operations for a <see cref="HiveProxy"/>.
    /// </summary>
    public sealed class ConsulManager : IDisposable
    {
        private object          syncRoot = new object();
        private HiveProxy       hive;
        private ConsulClient    client;

        /// <summary>
        /// Internal constructor.
        /// </summary>
        /// <param name="hive">The parent <see cref="HiveProxy"/>.</param>
        internal ConsulManager(HiveProxy hive)
        {
            Covenant.Requires<ArgumentNullException>(hive != null);

            this.hive = hive;
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
                        // Intentionally ignoring these.
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
        /// <para>
        /// Returns a Consul client.
        /// </para>
        /// <note>
        /// <b>WARNING:</b> The client returned is a shared resource and should 
        /// <b>NEVER BE DISPOSED</b>.
        /// </note>
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

                    client = HiveHelper.OpenConsul();
                }

                return client;
            }
        }
    }
}
