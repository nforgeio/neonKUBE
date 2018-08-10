//-----------------------------------------------------------------------------
// FILE:	    HiveHostNames.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Neon.Hive
{
    /// <summary>
    /// Defines the DNS hostnames used by built-in node level applications as well
    /// as Docker containers and services.
    /// </summary>
    /// <remarks>
    /// <para>
    /// DNS configuration for these hostnames may be included in node <b>hosts</b> files,
    /// Docker container [hosts] files and potentially other places such as the
    /// Consul DNS implementation.
    /// </para>
    /// <para>
    /// By convention, built-in DNS hostnames will be prefixed by <b>neon-*</b> and
    /// will have the <b>.hive</b> top-level domain.
    /// </para>
    /// </remarks>
    public static class HiveHostNames
    {
        /// <summary>
        /// The base DNS name used for all hive endpoints.
        /// </summary>
        public const string Base = "name.hive";     // $todo(jeff.lill): Delete this

        /// <summary>
        /// The base DNS name for the internal hive Docker registry cache instances deployed on the manager nodes.
        /// </summary>
        public const string RegistryCache = "neon-registry-cache.name.hive";

        /// <summary>
        /// The DNS name for the Elasticsearch containers used to store the hive logs.
        /// </summary>
        /// <remarks>
        /// <para>
        /// These are individual containers that attached to the <see cref="HiveConst.PrivateNetwork"/>,
        /// forming an Elasticsearch cluster that is deployed behind the hive's <b>private</b> proxy.  A DNS entry
        /// is configured in the each Docker node's <b>hosts</b> file to reference the node's IP address as well 
        /// as in the <b>/etc/neon/env-host</b> file that may be mounted into Docker containers and services.
        /// </para>
        /// <para>
        /// HTTP traffic should be directed to the <see cref="HiveHostPorts.ProxyPrivateHttpLogEsData"/> port which
        /// will be routed to the <b>neon-proxy-private</b> service via the Docker ingress network.
        /// </para>
        /// </remarks>
        public const string LogEsData = "neon-log-esdata.name.hive";

        /// <summary>
        /// The DNS name used to access the hive's HashiCorp Consul service.
        /// </summary>
        public const string Consul = "neon-consul.name.hive";

        /// <summary>
        /// The DNS name for the hive's HashiCorp Vault service.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Hive services access Vault using this hostname to take advantage of the <b>neon-proxy-vault</b>
        /// which provides for failover.
        /// </para>
        /// <para>
        /// This is also the base name for the manager node specific endpoints like
        /// <b><i>manager-name</i>.neon-vault.hive</b>, which are used by <b>neon-proxy-vault</b>
        /// to check instance health.
        /// </para>
        /// </remarks>
        public const string Vault = "neon-vault.name.hive";

        /// <summary>
        /// The special hostname used by the <b>HostsFixture</b> Xunit test fixture to verify
        /// that the local DNS resolver has picked up the changes.  This is not used for any
        /// other purpose.
        /// </summary>
        public const string UpdateHosts = "neon-hosts-fixture-modify.name.hive";
    }
}
