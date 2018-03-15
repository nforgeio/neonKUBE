//-----------------------------------------------------------------------------
// FILE:	    DnsEndpoint.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Net;
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
        /// Specifies the target host's IP address or FQDN or a target host group
        /// by specifying <b>group=NAME</b>.
        /// </summary>
        [JsonProperty(PropertyName = "Target", Required = Required.Always)]
        public string Target { get; set; }

        /// <summary>
        /// Optional endpoint health check URI.  This defaults to <c>null</c>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Set this to <c>null</c> to disable health checking.  neonCLUSTER supports a few
        /// types of target health checks, specified by the URI scheme specified in <see cref="CheckUri"/>:
        /// </para>
        /// <list type="table">
        /// <item>
        ///     <term><b>http://host:port/path</b></term>
        ///     <description>
        ///     HTTP request based checking.  The optional <see cref="CheckHost"/>,
        ///     and <see cref="CheckMethod"/> properties can be specified.  <b>2xx</b>
        ///     and <b>3xx</b> response codes indicate that the target is healthy.
        ///     </description>
        /// </item>
        /// <item>
        ///     <term><b>https://host:port</b></term>
        ///     <description>
        ///     HTTPS request based checking.  The optional <see cref="CheckHost"/>,
        ///     and <see cref="CheckMethod"/> properties can be specified.  <b>2xx</b>
        ///     and <b>3xx</b> response codes indicate that the target is healthy.
        ///     </description>
        /// </item>
        /// <item>
        ///     <term><b>tcp://host:port</b></term>
        ///     <description>
        ///     A socket connection is made to the <b>host/port</b>.  Targets that
        ///     allow connections are considered healthy.
        ///     </description>
        /// </item>
        /// <item>
        ///     <term><b>ping://host</b></term>
        ///     <description>
        ///     A few ICMP pings are sent to the target host.  Hosts that respond
        ///     are considered healthy.
        ///     </description>
        /// </item>
        /// </list>
        /// <note>
        /// <b>host</b> may be an IP address or a fully qualified domain name.
        /// </note>
        /// <para>
        /// No health checks are performed if <see cref="CheckUri"/> is <c>null</c>.  For this case,
        /// a record with the <see cref="Target"/> address will always be returned, regardless of 
        /// actual endpoint status.
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
        /// <b>host</b> in the URL can be specified as <b>@TARGET</b>, which specifies
        /// that <see cref="Target"/> or the target group host IP address will be substituted.
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

        /// <summary>
        /// Attempts to extract the node group name from <see cref="Target"/> if present.
        /// </summary>
        /// <returns>The group name or <c>null</c>.</returns>
        public string GetGroupName()
        {
            if (Target.StartsWith("group="))
            {
                return Target.Substring("group=".Length);
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Validates the DNS endpoint.  Any warning/errors will be appended to <paramref name="warnings"/>.
        /// </summary>
        /// <param name="warnings">Any warnings will be appended here.</param>
        /// <param name="clusterDefinition">The current cluster definition,</param>
        /// <param name="nodeGroups">The cluster node groups.</param>
        /// <param name="targetHostname">The parent target hostname.</param>
        public void Validate(List<string> warnings, ClusterDefinition clusterDefinition, Dictionary<string, List<NodeDefinition>> nodeGroups, string targetHostname)
        {
            Covenant.Requires<ArgumentException>(clusterDefinition != null);
            Covenant.Requires<ArgumentException>(nodeGroups != null);

            if (string.IsNullOrEmpty(Target))
            {
                warnings.Add($"Invalid [{nameof(DnsEndpoint)}.{nameof(Target)}={Target}] for [{nameof(DnsTarget)}={targetHostname}].");
            }

            var groupName = GetGroupName();

            if (groupName != null)
            {
                if (!string.IsNullOrEmpty(groupName))
                {
                    warnings.Add($"Invalid [{nameof(DnsEndpoint)}.{nameof(Target)}={Target}] for [{nameof(DnsTarget)}={targetHostname}].");
                }
                else if (!nodeGroups.ContainsKey(groupName))
                {
                    warnings.Add($"Node group [{groupName}] not found for [{nameof(DnsTarget)}={targetHostname}].");
                }
            }
            else
            {
                if (!IPAddress.TryParse(Target, out var address) && !ClusterDefinition.DnsHostRegex.IsMatch(Target))
                {
                    warnings.Add($"Invalid [{nameof(DnsEndpoint)}.{nameof(Target)}={Target}] is not a valid IP address or DNS hostname for [{nameof(DnsTarget)}={targetHostname}].");
                }
            }

            if (string.IsNullOrEmpty(CheckUri))
            {
                if (!Uri.TryCreate(CheckUri, UriKind.Absolute, out var uri))
                {
                    warnings.Add($"Invalid [{nameof(DnsEndpoint)}.{nameof(CheckUri)}={CheckUri}] is not a valid URI for [{nameof(DnsTarget)}={targetHostname}].");
                }
            }

            if (string.IsNullOrEmpty(CheckHost) && !ClusterDefinition.DnsHostRegex.IsMatch(CheckHost))
            {
                warnings.Add($"Invalid [{nameof(DnsEndpoint)}.{nameof(CheckHost)}={CheckHost}] is not a valid DNS hostname for [{nameof(DnsTarget)}={targetHostname}].");
            }
        }
    }
}
