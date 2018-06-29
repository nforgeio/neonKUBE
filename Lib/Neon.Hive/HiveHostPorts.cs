//-----------------------------------------------------------------------------
// FILE:	    HiveHostPorts.cs
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
    /// Defines the Docker host network ports in the <b>5000-5499</b> range reserved 
    /// by neonHIVE used by local services, containters and services on the ingress network.
    /// </summary>
    /// <remarks>
    /// <note>
    /// <b>IMPORTANT:</b> Do not change any of these values without really knowing what
    /// you're doing.  It's likely that these values have been literally embedded
    /// in cluster configuration scripts as well as Docker images.  Any change is likely
    /// to break things.
    /// </note>
    /// <note>
    /// <b>IMPORTANT:</b> These definitions must match those in the <b>$\Stack\Docker\Images\neonhive.sh</b>
    /// file.  You must manually update that file and then rebuild and push the containers
    /// as well as redeploy all clusters from scratch.
    /// </note>
    /// <para>
    /// These ports are organized into the following ranges:
    /// </para>
    /// <list type="table">
    /// <item>
    ///     <term><b>5000-5099</b></term>
    ///     <description>
    ///     Reserved for various native Linux services and Docker containers running
    ///     on the host.
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><b>5100-5299</b></term>
    ///     <description>
    ///     Reserved for services proxied by the <b>neon-proxy-public</b> service
    ///     on the <b>neon-public</b> network.
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><b>5300-5499</b></term>
    ///     <description>
    ///     Reserved for services proxied by the <b>neon-proxy-private</b> service
    ///     on the <b>neon-private</b> network.
    ///     </description>
    /// </item>
    /// </list>
    /// </remarks>
    public static class HiveHostPorts
    {
        /// <summary>
        /// The first reserved neonHIVE port.
        /// </summary>
        public const int First = 5000;

        /// <summary>
        /// The last reserved neonHIVE port.
        /// </summary>
        public const int Last = 5499;

        //---------------------------------------------------------------------
        // Cluster reserved ports.

        /// <summary>
        /// The local cluster Docker registry port.
        /// </summary>
        public const int DockerRegistryLocal = 5000;

        /// <summary>
        /// The HTTP port exposed by the manager <b>neon-registry-cache</b> containers.
        /// </summary>
        public const int DockerRegistryCache = 5001;

        /// <summary>
        /// The main neonHIVE dashboard.
        /// </summary>
        public const int Dashboard = 5002;

        /// <summary>
        /// The <b>neon-log-kibana</b> (Kibana) log analysis dashboard.
        /// </summary>
        public const int Kibana = 5003;

        /// <summary>
        /// The <b>neon-proxy-vault</b> service port used for routing HTTP traffic to the
        /// Vault servers running on the manager nodes.
        /// </summary>
        public const int ProxyVault = 5004;

        /// <summary>
        /// The public HTTP API port exposed by individual <b>neon-log-esdata-#</b>
        /// Elasticsearch log repository containers.
        /// </summary>
        public const int LogEsDataHttp = 5005;

        /// <summary>
        /// The TCP port exposed by individual <b>neon-log-esdata-#</b> Elasticsearch
        /// log repository containers for internal inter-node communication.
        /// </summary>
        public const int LogEsDataTcp = 5006;

        /// <summary>
        /// The UDP port exposed by the <b>neon-log-host</b> containers that receives
        /// SYSLOG events from the HAProxy based services and perhaps other sources.
        /// </summary>
        public const int LogHostSysLog = 5007;

        /// <summary>
        /// This port is reserved and must not be assigned to any service.  This is
        /// currently referenced by the manager load balancer rule for Azure deployments
        /// and it must not actually host a service.  See the <b>AzureHostingManager</b>
        /// source code for more information.
        /// </summary>
        public const int ReservedUnused = 5099;

        //---------------------------------------------------------------------
        // Ports [5100-5299] are reserved for the public proxy that routes
        // external traffic into the cluster.
        //
        // [5100-5102] are used to route general purpose HTTP/S traffic
        //             to both neonHIVE and application services.
        //
        // [5102-5109] are reserved for internal neonHIVE TCP routes.
        //
        // [5120-5299] are available for use by application services for TCP or
        //             HTTP/S traffic.

        /// <summary>
        /// The first port reserved for the public proxy.
        /// </summary>
        public const int ProxyPublicFirst = 5100;

        /// <summary>
        /// The last port reserved for the public proxy.
        /// </summary>
        public const int ProxyPublicLast = 5299;

        /// <summary>
        /// The <b>neon-proxy-public</b> service port for routing external HTTP
        /// (e.g. Internet) requests to services within the cluster.
        /// </summary>
        public const int ProxyPublicHttp = 5100;

        /// <summary>
        /// The <b>neon-proxy-public</b> service port for routing external HTTPS
        /// (e.g. Internet) requests to services within the cluster.
        /// </summary>
        public const int ProxyPublicHttps = 5101;

        /// <summary>
        /// The first <b>neon-proxy-public</b> port available for routing custom
        /// HTTP/S or TCP services.
        /// </summary>
        public const int ProxyPublicFirstUserPort = 5120;

        /// <summary>
        /// The first <b>neon-proxy-public</b> port available for routing custom
        /// HTTP/S or TCP services.
        /// </summary>
        public const int ProxyPublicLastUserPort = 5299;

        //---------------------------------------------------------------------
        // Ports [5300-5499] are reserved for the private cluster proxy.
        //
        // [5300-5301] are used to route general purpose HTTP/S traffic
        //             to both neonHIVE and application services.
        //
        // [5302-5309] are reserved for internal neonHIVE TCP routes.
        //
        // [5320-5499] are available for use by application services for TCP or
        //             HTTP/S traffic.

        /// <summary>
        /// The first port reserved for the private proxy.
        /// </summary>
        public const int ProxyPrivateFirst = 5300;

        /// <summary>
        /// The last port reserved for the private proxy.
        /// </summary>
        public const int ProxyPrivateLast = 5499;

        /// <summary>
        /// The <b>neon-proxy-private</b> port for routing internal HTTP traffic.  
        /// This typically used to load balance traffic to stateful services that
        /// can't be deployed as Docker swarm mode services.
        /// </summary>
        public const int ProxyPrivateHttp = 5300;

        /// <summary>
        /// The <b>neon-proxy-private</b> port for routing internal HTTPS traffic.  
        /// This typically used to load balance traffic to stateful services that
        /// can't be deployed as Docker swarm mode services.
        /// </summary>
        public const int ProxyPrivateHttps = 5301;

        /// <summary>
        /// The <b>neon-proxy-private</b> port for routing internal TCP traffic
        /// to forward log events from the <b>neon-log-host</b> containers running on 
        /// the nodes to the <b>neon-log-collector</b> service.
        /// </summary>
        public const int ProxyPrivateTcpLogCollector = 5302;

        /// <summary>
        /// The <b>neon-proxy-private</b> port for routing internal HTTP traffic
        /// to the logging Elasticsearch cluster.
        /// </summary>
        public const int ProxyPrivateHttpLogEsData = 5303;

        /// <summary>
        /// The port assigned to the cluster Ceph web dashboard.
        /// </summary>
        public const int ProxyPrivateHttpCephDashboard = 5304;

        /// <summary>
        /// The port assigned to the Kibana dashboard.
        /// </summary>
        public const int ProxyPrivateHttpKibana = 5305;

        /// <summary>
        /// The first <b>neon-proxy-private</b> port available for routing custom
        /// HTTP/S or TCP services.
        /// </summary>
        public const int ProxyPrivateFirstUserPort = 5320;

        /// <summary>
        /// The first <b>neon-proxy-private</b> port available for routing custom
        /// HTTP/S or TCP services.
        /// </summary>
        public const int ProxyPrivateLastUserPort = 5499;
    }
}
