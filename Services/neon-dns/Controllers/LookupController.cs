//-----------------------------------------------------------------------------
// FILE:	    LookupController.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2017 by neonFORGE, LLC.  All rights reserved.

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
        /// Returns a 200 status code when the service is healthy.
        /// </summary>
        /// <param name="qname">The DNS hostname being queried.</param>
        /// <param name="qtype">The DNS query record type (or <b>ANY</b>).</param>
        [HttpGet("/lookup/{qname}/{qtype}")]
        public async Task<object> Get(string qname, string qtype)
        {
            Console.WriteLine($"{qname} {qtype}");

            // Strip off any terminating "." from the query name.

            if (qname.EndsWith("."))
            {
                qname = qname.Substring(0, qname.Length - 1);
            }

            DnsResult result;

            switch (qtype)
            {
                case "SOA":

                    // We need to answer SOA requests for [cluster].

                    if (qname.Equals("cluster", StringComparison.InvariantCultureIgnoreCase))
                    {
                        result = new DnsResult();

                        result.Result.Add(
                            new DnsAnswer()
                            {
                                Ttl   = 3600,
                                QName = qname,
                                QType = qtype
                            });
                    }
                    else
                    {
                        return DnsResult.Empty;
                    }
                    break;

                case "A":
                case "ANY":

                    result = new DnsResult();

                    if (qname.Equals("test.cluster", StringComparison.InvariantCultureIgnoreCase))
                    {
                        result.Result.Add(
                        new DnsAnswer()
                        {
                            Ttl     = 300,
                            QName   = qname,
                            QType   = "A",
                            Content = $"10.0.0.2"
                        });
                    }
                    else
                    {
                        return DnsResult.Empty;
                    }
                    break;

                default:

                    return DnsResult.Empty;
            }

            return result;
        }
    }
}
