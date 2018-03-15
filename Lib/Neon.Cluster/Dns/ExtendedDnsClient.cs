//-----------------------------------------------------------------------------
// FILE:	    ExtendedDnsClient.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Net;
using System.Threading.Tasks;

using DNS.Client;

using Neon.Common;
using Neon.Cluster;
using System.Threading;

namespace Neon.DnsTools
{
    /// <summary>
    /// Extends <see cref="DnsClient"/> to support resolution against 
    /// multiple name servers.
    /// </summary>
    public class ExtendedDnsClient
    {
        private DnsClient[]     clients;

        /// <summary>
        /// Constructs an instance to query one or more nameserver IP addresses.
        /// </summary>
        /// <param name="nameservers">The name server IP addresses (at least one must be passed).</param>
        public ExtendedDnsClient(params IPAddress[] nameservers)
        {
            Covenant.Requires<ArgumentException>(nameservers != null && nameservers.Length != 0, "At least one nameserver is required.");

            clients = new DnsClient[nameservers.Length];

            for (int i = 0; i < nameservers.Length; i++)
            {
                clients[i] = new DnsClient(nameservers[i]);
            }
        }

        /// <summary>
        /// Constructs an instance to query one or more nameserver IP address strings.
        /// </summary>
        /// <param name="nameservers">The name server IP addresses (at least one must be passed).</param>
        public ExtendedDnsClient(params string[] nameservers)
        {
            Covenant.Requires<ArgumentException>(nameservers != null && nameservers.Length != 0, "At least one nameserver is required.");

            clients = new DnsClient[nameservers.Length];

            for (int i = 0; i < nameservers.Length; i++)
            {
                clients[i] = new DnsClient(nameservers[i]);
            }
        }

        /// <summary>
        /// Attempts to resolve an IP address or fully qualified domain name
        /// into host IP addresses.
        /// </summary>
        /// <param name="addressOrFQDN">The IP address or FQDN.</param>
        /// <returns>An empty result set if the lookup failed.</returns>
        public async Task<IEnumerable<IPAddress>> LookupAsync(string addressOrFQDN)
        {
            // We can short-circuit things if the parameter is an IP address.

            if (IPAddress.TryParse(addressOrFQDN, out var address))
            {
                return new IPAddress[] { address };
            }

            // We're going to submit requests to all DNS clients in parallel and
            // return replies from the first one that answers.

            var readyEvent = new AsyncAutoResetEvent();
            var sync       = new object();
            var pending    = clients.Length;
            var results    = (IList<IPAddress>)null;

            foreach (var client in clients)
            {
                var task = Task.Run(
                    async () =>
                    {
                        try
                        {
                            var addresses = await client.Lookup(addressOrFQDN);

                            if (addresses.Count > 0)
                            {
                                lock (sync)
                                {
                                    if (results == null)
                                    {
                                        results = addresses;
                                    }
                                }
                            }

                            Interlocked.Decrement(ref pending);
                            readyEvent.Set();
                        }
                        catch (ResponseException)
                        {
                            // $todo(jeff.lill): 
                            //
                            // I wish the underlying client didn't throw exceptions.  Perhaps
                            // I could extend the implementation to implement [TryResolve()].

                            Interlocked.Decrement(ref pending);
                            readyEvent.Set();
                        }
                    });
            }

            while (pending > 0)
            {
                await readyEvent.WaitAsync();

                if (results != null)
                {
                    return results;
                }
            }

            return null;
        }
    }
}
