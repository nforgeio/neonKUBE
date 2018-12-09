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
    /// in hive configuration scripts as well as Docker images.  Any change is likely
    /// to break things.
    /// </note>
    /// <note>
    /// <b>IMPORTANT:</b> These definitions must match those in the <b>$\Stack\Docker\Images\neonhive.sh</b>
    /// file.  You must manually update that file and then rebuild and push the containers
    /// as well as redeploy all hives from scratch.
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
    ///     <term><b>80/443</b></term>
    ///     <description>
    ///     Reserved HTTP/HTTPS services proxied by the <b>neon-proxy-public</b> 
    ///     service on the <b>neon-public</b> network.
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
        // Hive reserved ports.

        /// <summary>
        /// The local hive Docker registry port.
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
        /// The public HTTP API port exposed by individual <b>neon-log-esdata</b>
        /// Elasticsearch log repository container instances.
        /// </summary>
        public const int LogEsDataHttp = 5005;

        /// <summary>
        /// The TCP port exposed by individual <b>neon-log-esdata</b> Elasticsearch
        /// log repository container instances for internal inter-node communication.
        /// </summary>
        public const int LogEsDataTcp = 5006;

        /// <summary>
        /// The UDP port exposed by the <b>neon-log-host</b> containers that receives
        /// SYSLOG events from the HAProxy based services and perhaps other sources.
        /// </summary>
        public const int LogHostSysLog = 5007;

        /// <summary>
        /// The port exposed by the hive's Ceph dashboard.
        /// </summary>
        public const int CephDashboard = 5008;

        /// <summary>
        /// The RabbitMQ/Erlang peer discovery protocol port.
        /// </summary>
        public const int HiveMQEPMD = 5009;

        /// <summary>
        /// The RabbitMQ message broker AMQP port.
        /// </summary>
        public const int HiveMQAMQP = 5010;

        /// <summary>
        /// The RabbitMQ message broker cluster internal communication port.
        /// </summary>
        public const int HiveMQDIST = 5011;

        /// <summary>
        /// The RabbitMQ management plugin port.  This serves the management
        /// REST API as well as the dashboard.
        /// </summary>
        public const int HiveMQManagement = 5012;

        /// <summary>
        /// This port is reserved and must not be assigned to any service.  This is
        /// currently referenced by the manager traffic manager rule for Azure deployments
        /// and it must not actually host a service.  See the <b>AzureHostingManager</b>
        /// source code for more information.
        /// </summary>
        public const int ReservedUnused = 5099;

        //---------------------------------------------------------------------
        // Ports [80/443] and [5100-5299] are reserved for the public proxy that 
        // routes external traffic into the hive.
        //
        // [80/443]    are used to route external HTTP/HTTPS traffic
        //             into the hive.
        //
        // [5100-5102] are used to route general purpose TCP and specialized 
        //             HTTP/S traffic to application services.
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
        /// (e.g. Internet) requests to services within the hive.
        /// </summary>
        public const int ProxyPublicHttp = 80;

        /// <summary>
        /// The <b>neon-proxy-public</b> service port for routing external HTTPS
        /// (e.g. Internet) requests to services within the hive.
        /// </summary>
        public const int ProxyPublicHttps = 443;

        /// <summary>
        /// The first <b>neon-proxy-public</b> port available for routing custom
        /// HTTP/S or TCP services.
        /// </summary>
        public const int ProxyPublicFirstUser = 5120;

        /// <summary>
        /// The first <b>neon-proxy-public</b> port available for routing custom
        /// HTTP/S or TCP services.
        /// </summary>
        public const int ProxyPublicLastUser = ProxyPublicLast;

        //---------------------------------------------------------------------
        // Ports [5300-5499] are reserved for the private hive proxy.
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
        /// The port assigned to the hive Ceph web dashboard.
        /// </summary>
        public const int ProxyPrivateHttpCephDashboard = 5304;

        /// <summary>
        /// The port assigned to the load balanced Kibana dashboard.
        /// </summary>
        public const int ProxyPrivateKibanaDashboard = 5305;

        /// <summary>
        /// The port assigned to the load balanced HiveMQ AMQP endpoint.
        /// </summary>
        public const int ProxyPrivateHiveMQAMQP = 5306;

        /// <summary>
        /// The port assigned to the load balanced HiveMQ management 
        /// plugin.  This serves the management REST API as well as
        /// the dashboard.
        /// </summary>
        public const int ProxyPrivateHiveMQAdmin = 5307;

        /// <summary>
        /// The first <b>neon-proxy-private</b> port available for routing custom
        /// HTTP/S or TCP services.
        /// </summary>
        public const int ProxyPrivateFirstUser = 5320;

        /// <summary>
        /// The first <b>neon-proxy-private</b> port available for routing custom
        /// HTTP/S or TCP services.
        /// </summary>
        public const int ProxyPrivateLastUser = ProxyPrivateLast;
    }
}
