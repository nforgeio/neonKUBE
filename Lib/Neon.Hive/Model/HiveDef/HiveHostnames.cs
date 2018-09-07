//-----------------------------------------------------------------------------
// FILE:	    HiveHostnames.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

using Neon.Common;
using Neon.Cryptography;
using Neon.IO;
using Neon.Net;

namespace Neon.Hive
{
    /// <summary>
    /// Returns the b uilt-in hostnames for a hive.
    /// </summary>
    /// <remarks>
    /// <para>
    /// By convention, built-in DNS hostnames will look like <b>NAME.HIVENAME.nhive.io</b>,
    /// where <b>NAME</b> identifies the target service and <b>HIVENAME</b> is the name
    /// of the hive.
    /// </para>
    /// </remarks>
    public class HiveHostnames
    {
        private HiveDefinition hiveDefinition;

        /// <summary>
        /// Internal constructor.
        /// </summary>
        /// <param name="hiveDefinition">The parent <see cref="HiveDefinition"/>.</param>
        internal HiveHostnames(HiveDefinition hiveDefinition)
        {
            Covenant.Requires<ArgumentNullException>(hiveDefinition != null);

            this.hiveDefinition = hiveDefinition;
        }

        /// <summary>
        /// Returns the base hostname used for all hive endpoints.
        /// </summary>
        public string Base => $"{hiveDefinition.Name}.nhive.io";

        /// <summary>
        /// Returns the base hostname for the internal hive Docker registry cache instances deployed on the manager nodes.
        /// </summary>
        public string RegistryCache => $"neon-registry-cache.{hiveDefinition.Name}.nhive.io";

        /// <summary>
        /// Returns the DNS name for the Elasticsearch containers used to store the hive logs.
        /// </summary>
        /// <remarks>
        /// <para>
        /// These are individual containers that attached to the <see cref="HiveConst.PrivateNetwork"/>,
        /// forming an Elasticsearch cluster that is deployed behind the hive's <b>private</b> proxy.  A DNS entry
        /// is configured in the each Docker node's <b>hosts</b> file to reference the node's IP address as well 
        /// as in the <b>/etc/neon/host-env</b> file that may be mounted into Docker containers and services.
        /// </para>
        /// <para>
        /// HTTP traffic should be directed to the <see cref="HiveHostPorts.ProxyPrivateHttpLogEsData"/> port which
        /// will be routed to the <b>neon-proxy-private</b> service via the Docker ingress network.
        /// </para>
        /// </remarks>
        public string LogEsData => $"neon-log-esdata.{hiveDefinition.Name}.nhive.io";

        /// <summary>
        /// Returns the hostname used to access the hive's HashiCorp Consul service.
        /// </summary>
        public string Consul => $"neon-consul.{hiveDefinition.Name}.nhive.io";

        /// <summary>
        /// Returns the hostname for the hive's HashiCorp Vault service.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Hive services access Vault using this hostname to take advantage of the <b>neon-proxy-vault</b>
        /// which provides for failover.
        /// </para>
        /// <para>
        /// This is also the base name for the manager node specific endpoints like
        /// <b><i>manager-name</i>.neon-vault.HIVENAME.nhive.io</b>, which are used by <b>neon-proxy-vault</b>
        /// to check instance health.
        /// </para>
        /// </remarks>
        public string Vault => $"neon-vault.{hiveDefinition.Name}.nhive.io";

        /// <summary>
        /// Returns the base hostname for the internal hive Docker RabbitMQ cluster nodes.
        /// </summary>
        /// <remarks>
        /// Individual RabbitMQ instances will be named like <b><i>hive-node</i>.neon-hive.HIVENAME.nhive.io</b>,
        /// where <i>hive-node</i> is the name of the hive node hosting the component.
        /// </remarks>
        public string HiveMQ => $"neon-hivemq.{hiveDefinition.Name}.nhive.io";

        /// <summary>
        /// Returns the special hostname used by the <b>HostsFixture</b> Xunit test fixture to verify
        /// that the local DNS resolver has picked up the changes.  This is not used for any
        /// other purpose.
        /// </summary>
        public string UpdateHosts => $"neon-hosts-fixture-modify.{hiveDefinition.Name}.nhive.io";
    }
}
