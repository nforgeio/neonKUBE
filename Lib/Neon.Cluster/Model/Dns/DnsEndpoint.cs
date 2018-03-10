//-----------------------------------------------------------------------------
// FILE:	    DnsEndpoint.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Neon.Net;

namespace Neon.Cluster
{
    /// <summary>
    /// Describes a DNS endpoint made by a <see cref="DnsDomain"/>.
    /// </summary>
    public class DnsEndpoint
    {
        /// <summary>
        /// The target host's IP address or FQDN.
        /// </summary>
        public string Target { get; set; }

        /// <summary>
        /// The integer priority for <b>MX</b>, <b>SRV</b>... records.
        /// </summary>
        public string Priority { get; set; }

        /// <summary>
        /// The record time-to-live in seconds (defaults to 60).
        /// </summary>
        public int Ttl { get; set; } = 60;

        /// <summary>
        /// The optional endpoint health check URI.  (defaults to <c>null</c>)
        /// </summary>
        /// <remarks>
        /// <para>
        /// Set this to <c>null</c> to disable health checking.  HTTP and TCP health checks are
        /// both supported.  Specify a <b>http://</b> URI for an HTTP based health check or 
        /// <b>tcp://</b> URI for a TCP check.
        /// </para>
        /// <para>
        /// No health checks are performed if checking is disabled.  In this case, a record with
        /// <see cref="Target"/> address will always be returned, regardless of actual endpoint status.
        /// </para>
        /// <para>
        /// Endpoints with URIs like <b>http://host:port/path</b> will be verified by making
        /// an HTTP GET request by default.  The endpoint is considered to be healthy for
        /// 2xx and 3xx response status codes.  You can set <see cref="CheckMethod"/> to
        /// override the HTTP method used.
        /// </para>
        /// <para>
        /// Endpoints with URIs like <b>tcp://host:port</b> will be verified by attempting
        /// to establish a TCP connection.  Endpoints that connect are considered to
        /// be healthy.
        /// </para>
        /// <note>
        /// <b>host</b> in the URL can be specified as <b>@@TARGET</b>, which specifies
        /// that <see cref="Target"/> will be substituted.
        /// </note>
        /// </remarks>
        public string CheckUri { get; set; }

        /// <summary>
        /// The HTTP method to use for HTTP health checks. (defaults to <b>GET</b>)
        /// </summary>
        public string CheckMethod { get; set; } = "GET";

        /// <summary>
        /// The host name to use 
        /// </summary>
        public string CheckHost { get; set; }
    }
}
