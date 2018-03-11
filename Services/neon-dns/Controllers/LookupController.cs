//-----------------------------------------------------------------------------
// FILE:	    LookupController.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Consul;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

using Neon.Common;
using Neon.Cluster;
using Neon.Diagnostics;
using Neon.Web;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

// This PowerDNS link describes how backends work:
//
//      https://blog.powerdns.com/2015/06/23/what-is-a-powerdns-backend-and-how-do-i-make-it-send-an-nxdomain/

// $todo(jeff.lill):
//
// This implementation is currently hardcoded to answer requests only for
// the [cluster] domain.

namespace NeonDns.Controllers
{
    /// <summary>
    /// Health endpoint for the load balancer proxy.
    /// </summary>
    public class LookupController : NeonController
    {
        private readonly ConsulClient consul;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="consul">The Consul client used to query for dynamic DNS records  (dependency injected).</param>
        public LookupController(ConsulClient consul)
        {
            this.consul = consul;
        }

        /// <summary>
        /// Performs a dynamic DNS lookup from answers persisted to Consul by the
        /// <b>neon-dns-health</b> service.
        /// </summary>
        /// <param name="qname">The DNS hostname being queried.</param>
        /// <param name="qtype">The DNS query record type (or <b>ANY</b>).</param>
        [HttpGet("/lookup/{qname}/{qtype}")]
        public async Task<object> Get(string qname, string qtype)
        {
            // Strip off any terminating "." from the query name.

            if (qname.EndsWith("."))
            {
                qname = qname.Substring(0, qname.Length - 1);
            }

            PdnsDnsResult result;

            switch (qtype)
            {
                case "SOA":

                    // We need to answer DNS requests for 

                    if (qname.Equals("cluster", StringComparison.InvariantCultureIgnoreCase))
                    {
                        result = new PdnsDnsResult();

                        result.Result.Add(
                            new PdnsDnsAnswer()
                            {
                                Ttl   = 3600,
                                QName = qname,
                                QType = qtype
                            });
                    }
                    else
                    {
                        return PdnsDnsResult.Empty;
                    }
                    break;

                case "A":
                case "ANY":

                    result = new PdnsDnsResult();

                    if (qname.Equals("test.cluster", StringComparison.InvariantCultureIgnoreCase))
                    {
                        result.Result.Add(
                        new PdnsDnsAnswer()
                        {
                            Ttl     = 300,
                            QName   = qname,
                            QType   = "A",
                            Content = $"10.0.0.2"
                        });
                    }
                    else
                    {
                        return PdnsDnsResult.Empty;
                    }
                    break;

                default:

                    return PdnsDnsResult.Empty;
            }

            return result;
        }

        /// <summary>
        /// Queries Consul to resolve a DNS host and type.
        /// </summary>
        /// <param name="qname">The query domain.</param>
        /// <param name="qtype">The query type.</param>
        /// <returns>The <see cref="DnsAnswer"/> if found, <c>null</c> otherwise.</returns>
        private Task<DnsAnswer> FindDnsAnswerAsync(string qname, string qtype)
        {
            // Convert the name to lowercase and the type to uppercase
            // and generate the key used to persist the record to Consul.

            qname = qname.ToLowerInvariant();
            qtype = qtype.ToUpperInvariant();

            var key = $"{NeonClusterConst.DnsConsulAnswersKey}/{qname}-{qtype}";

            var result = new DnsAnswer()
            {
                Domain   = qname,
                Type     = qtype,
                Contents = "17.0.0.1",
                Ttl      = 10
            };

            return Task.FromResult(result);
        }
    }
}
