//-----------------------------------------------------------------------------
// FILE:	    DnsEndpoint.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

using Neon.Net;

namespace Neon.Cluster
{
    /// <summary>
    /// Describes a DNS endpoint made by a <see cref="DnsTarget"/>.
    /// </summary>
    public class DnsEndpoint
    {
        /// <summary>
        /// The target host's IP address or FQDN.
        /// </summary>
        [JsonProperty(PropertyName = "Target", Required = Required.Always)]
        public string Target { get; set; }

        /// <summary>
        /// Optional endpoint health check URI.  This defaults to <c>null</c>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Set this to <c>null</c> to disable health checking.  HTTP and TCP health checks are
        /// both supported.  Specify a <b>http://</b> URI for an HTTP based health check or 
        /// <b>tcp://</b> URI for a TCP check.
        /// </para>
        /// <para>
        /// No health checks are performed if checking is disabled.  In this case, a record with
        /// the <see cref="Target"/> address will always be returned, regardless of actual endpoint
        /// status.
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
        [JsonProperty(PropertyName = "CheckUri", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string CheckUri { get; set; }

        /// <summary>
        /// Optional HTTP method to use for HTTP health checks.  This defaults to <b>GET</b>.
        /// </summary>
        [JsonProperty(PropertyName = "CheckMethod", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue("GET")]
        public string CheckMethod { get; set; } = "GET";

        /// <summary>
        /// Optional host name to present when making a HTTP health check request to this 
        /// target.  This defaults to <c>null</c> which means that <see cref="Target"/>
        /// will be submitted as the host name.
        /// </summary>
        [JsonProperty(PropertyName = "CheckHost", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string CheckHost { get; set; }
    }
}
