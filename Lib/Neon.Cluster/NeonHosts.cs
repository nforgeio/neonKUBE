//-----------------------------------------------------------------------------
// FILE:	    NeonHosts.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Neon.Cluster
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
    /// will have the <b>.cluster</b> top-level domain.
    /// </para>
    /// </remarks>
    public static class NeonHosts
    {
        /// <summary>
        /// The base DNS name for the internal cluster Docker registry cache instances deployed on the manager nodes.
        /// </summary>
        public const string RegistryCache = "neon-registry-cache.cluster";

        /// <summary>
        /// The DNS name for the Elasticsearch containers used to store the cluster logs.
        /// </summary>
        /// <remarks>
        /// <para>
        /// These are individual containers that attached to the <see cref="NeonClusterConst.PrivateNetwork"/>,
        /// forming an Elasticsearch cluster that is deployed behind the cluster's <b>private</b> proxy.  A DNS entry
        /// is configured in the each Docker node's <b>hosts</b> file to reference the node's IP address as well 
        /// as in the <b>/etc/neoncluster/env-host</b> file that may be mounted into Docker containers and services.
        /// </para>
        /// <para>
        /// HTTP traffic should be directed to the <see cref="NeonHostPorts.ProxyPrivateHttpLogEsData"/> port which
        /// will be routed to the <b>neon-proxy-private</b> service via the Docker ingress network.
        /// </para>
        /// </remarks>
        public const string LogEsData = "neon-log-esdata.cluster";

        /// <summary>
        /// The DNS name used to access the cluster's HashiCorp Consul service.
        /// </summary>
        public const string Consul = "neon-consul.cluster";

        /// <summary>
        /// The DNS name for the cluster's HashiCorp Vault service.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Cluster services access Vault using this hostname to take advantage of the <b>neon-proxy-vault</b>
        /// which provides for failover.
        /// </para>
        /// <para>
        /// This is also the base name for the manager node specific endpoints like
        /// <b><i>manager-name</i>.neon-vault.cluster</b>, which are used by <b>neon-proxy-vault</b>
        /// to check instance health.
        /// </para>
        /// </remarks>
        public const string Vault = "neon-vault.cluster";
    }
}
