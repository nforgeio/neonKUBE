//-----------------------------------------------------------------------------
// FILE:	    HiveMQSettings.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.IO;

using EasyNetQ;
using EasyNetQ.DI;
using EasyNetQ.Logging;
using EasyNetQ.Management.Client;

using RabbitMQ;
using RabbitMQ.Client;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using YamlDotNet.Serialization;

using Neon.Common;
using Neon.Diagnostics;
using Neon.Net;

namespace Neon.HiveMQ
{
    /// <summary>
    /// Settings used to connect a RabbitMQ client to a message broker.
    /// </summary>
    public class HiveMQSettings
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Parses a <see cref="HiveMQSettings"/> from a JSON or YAML string,
        /// automatically detecting the input format.
        /// </summary>
        /// <param name="jsonOrYaml"></param>
        /// <param name="strict">Optionally require that all input properties map to route properties.</param>
        /// <returns>The parsed <see cref="HiveMQSettings"/>.</returns>
        public static HiveMQSettings Parse(string jsonOrYaml, bool strict = false)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(jsonOrYaml));

            return NeonHelper.JsonOrYamlDeserialize<HiveMQSettings>(jsonOrYaml, strict);
        }

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// Specifies the virtual host namespace.  This defaults to the root virtual host <b>"/"</b>.
        /// </summary>
        [JsonProperty(PropertyName = "VirtualHost", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string VirtualHost { get; set; } = "/";

        /// <summary>
        /// Specifies the hostnames or IP addresses of the RabbitMQ nodes available for
        /// handling AMQP protocol requests.  These endpoints must be listening on
        /// <see cref="AmqpPort"/>.
        /// </summary>
        /// <remarks>
        /// You must specify the hostname/address for at least one operating RabbitMQ node.  
        /// The RabbitMQ client will use this to discover the remaining nodes.  It is a best 
        /// practice to specify multiple nodes in a clustered environment to avoid initial
        /// connection problems when any single node is down or to your RabbitMQ nodes
        /// hosted behind a traffic manager.
        /// </remarks>
        [JsonProperty(PropertyName = "AmqpHosts", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public List<string> AmqpHosts { get; set; } = new List<string>();

        /// <summary>
        /// Specifies the broker port number.  This defaults to <b>5672</b>.
        /// </summary>
        [JsonProperty(PropertyName = "AmqpPort", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(NetworkPorts.AMQP)]
        public int AmqpPort { get; set; } = NetworkPorts.AMQP;

        /// <summary>
        /// Specifies the hostnames or IP addresses of the RabbitMQ nodes available for
        /// handling management REST API requests.  These endpoints must be listening on
        /// <see cref="AdminPort"/>.
        /// </summary>
        /// <remarks>
        /// You must specify the hostname/address for at least one operating RabbitMQ node.  
        /// The RabbitMQ client will use this to discover the remaining nodes.  It is a best 
        /// practice to specify multiple nodes in a clustered environment to avoid initial
        /// connection problems when any single node is down.
        /// </remarks>
        [JsonProperty(PropertyName = "AdminHosts", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public List<string> AdminHosts { get; set; } = new List<string>();

        /// <summary>
        /// Specifies the broker port number.  This defaults to <b>15672</b>.
        /// </summary>
        [JsonProperty(PropertyName = "AdminPort", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(NetworkPorts.RabbitMQAdmin)]
        public int AdminPort { get; set; } = NetworkPorts.RabbitMQAdmin;

        /// <summary>
        /// Enables TLS.  This defaults to <c>false</c>.
        /// </summary>
        [JsonProperty(PropertyName = "TlsEnabled", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(false)]
        public bool TlsEnabled { get; set; } = false;

        /// <summary>
        /// The username used to authenticate against RabbitMQ.
        /// </summary>
        [JsonProperty(PropertyName = "Username", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string Username { get; set; }

        /// <summary>
        /// The password used to authenticate against RabbitMQ.
        /// </summary>
        [JsonProperty(PropertyName = "Password", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string Password { get; set; }

        /// <summary>
        /// Indicates that standard <see cref="LogManager"/> based logging will be
        /// configured for the EasyNetQ client.  This defaults to <c>true</c>.
        /// </summary>
        [JsonProperty(PropertyName = "NeonLog", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(true)]
        public bool NeonLog { get; set; } = true;

        /// <summary>
        /// Returns <c>true</c> if the settings are valid.
        /// </summary>
        [JsonIgnore]
        [YamlIgnore]
        public bool IsValid
        {
            get
            {
                if (string.IsNullOrEmpty(VirtualHost))
                {
                    return false;
                }

                // Verify the AMQP hosts and port.

                if (AmqpHosts == null || AmqpHosts.Count == 0)
                {
                    return false;
                }

                foreach (var hostname in AmqpHosts)
                {
                    if (string.IsNullOrEmpty(hostname))
                    {
                        return false;
                    }
                }

                if (!NetHelper.IsValidPort(AmqpPort))
                {
                    return false;
                }

                // Verify the management hosts and port.

                if (AdminHosts == null || AdminHosts.Count == 0)
                {
                    return false;
                }

                foreach (var hostname in AdminHosts)
                {
                    if (string.IsNullOrEmpty(hostname))
                    {
                        return false;
                    }
                }

                if (!NetHelper.IsValidPort(AdminPort))
                {
                    return false;
                }

                return true;
            }
        }

        /// <summary>
        /// Returns a RabbitMQ cluster connection using specified settings, optionally overriding
        /// the username and password.
        /// </summary>
        /// <param name="username">Optional username.</param>
        /// <param name="password">Optional password.</param>
        /// <param name="dispatchConsumersAsync">Optionally enables <c>async</c> message consumers.  This defaults to <c>false</c>.</param>
        /// <returns>The RabbitMQ <see cref="IConnection"/>.</returns>
        public IConnection ConnectRabbitMQ(string username = null, string password = null, bool dispatchConsumersAsync = false)
        {
            Covenant.Requires<ArgumentNullException>(AmqpHosts != null && AmqpHosts.Count > 0);

            var connectionFactory = new ConnectionFactory();

            connectionFactory.VirtualHost            = VirtualHost;
            connectionFactory.UserName               = username ?? Username;
            connectionFactory.Password               = password ?? Password;
            connectionFactory.Port                   = AmqpPort;
            connectionFactory.DispatchConsumersAsync = dispatchConsumersAsync;

            if (string.IsNullOrEmpty(connectionFactory.UserName))
            {
                throw new ArgumentNullException($"[{nameof(username)}] is required.");
            }

            if (string.IsNullOrEmpty(connectionFactory.Password))
            {
                throw new ArgumentNullException($"[{nameof(password)}] is required.");
            }

            if (TlsEnabled)
            {
                connectionFactory.Ssl = new SslOption() { Enabled = true };
            }

            return new RabbitMQConnection(connectionFactory.CreateConnection(AmqpHosts));
        }

        /// <summary>
        /// Returns a RabbitMQ cluster connection using specified settings and credentials.
        /// </summary>
        /// <param name="credentials">The credentials.</param>
        /// <param name="dispatchConsumersAsync">Optionally enables <c>async</c> message consumers.  This defaults to <c>false</c>.</param>
        /// <returns>The RabbitMQ <see cref="IConnection"/>.</returns>
        public IConnection ConnectRabbitMQ(Credentials credentials, bool dispatchConsumersAsync = false)
        {
            Covenant.Requires<ArgumentNullException>(credentials != null);
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(VirtualHost));
            Covenant.Requires<ArgumentNullException>(AmqpHosts != null && AmqpHosts.Count > 0);

            return ConnectRabbitMQ(credentials.Username, credentials.Password, dispatchConsumersAsync);
        }

        /// <summary>
        /// Returns an EasyNetQ <see cref="IBus"/> connection to a RabbitMQ cluster.
        /// </summary>
        /// <param name="username">Optional username (overrides <see cref="Username"/>).</param>
        /// <param name="password">Optional password (overrides <see cref="Password"/>).</param>
        /// <param name="virtualHost">Optional target virtual host (defaults to <b>"/"</b>).</param>
        /// <param name="busSettings">Optional message bus client settings.</param>
        /// <param name="customServiceAction">
        /// Optionally specifies an action that overrides the default the EasyNetQ
        /// client configuration via dependency injection.
        /// </param>
        /// <returns>The connected <see cref="IBus"/>.</returns>
        public IBus ConnectEasyNetQ(
            string                      username = null, 
            string                      password = null, 
            string                      virtualHost = "/", 
            EasyBusSettings             busSettings = null, 
            Action<IServiceRegister>    customServiceAction = null)
        {
            Covenant.Requires<NotImplementedException>(!TlsEnabled, "$todo(jeff.lill): We don't support RabbitMQ TLS yet.");

            var config = new ConnectionConfiguration()
            {
                Port        = (ushort)AmqpPort,
                UserName    = username ?? Username,
                Password    = password ?? Password,
                VirtualHost = virtualHost
            };

            if (busSettings != null)
            {
                busSettings.ApplyTo(config);
            }

            if (string.IsNullOrEmpty(config.UserName))
            {
                throw new ArgumentNullException($"[{nameof(username)}] is required.");
            }

            if (string.IsNullOrEmpty(config.Password))
            {
                throw new ArgumentNullException($"[{nameof(password)}] is required.");
            }

            var hostConfigs = new List<HostConfiguration>();

            foreach (var host in AmqpHosts)
            {
                hostConfigs.Add(new HostConfiguration() { Host = host, Port = (ushort)AmqpPort });
            }

            config.Hosts = hostConfigs;

            // Enable Neon based logging if requested (which is the default).

            if (NeonLog)
            {
                // Generate a reasonable [sourceModule] setting.

                var sourceModule = "EasyNetQ";
                var product      = busSettings?.Product ?? string.Empty;
                var appName      = busSettings?.Name ?? string.Empty;

                if (!string.IsNullOrEmpty(product) || !string.IsNullOrEmpty(appName))
                {
                    if (string.IsNullOrEmpty(product))
                    {
                        sourceModule = appName;
                    }
                    else if (string.IsNullOrEmpty(appName))
                    {
                        sourceModule = product;
                    }
                    else if (product != appName)
                    {
                        sourceModule = $"{product}/{appName}";
                    }
                    else
                    {
                        sourceModule = appName;
                    }
                }
                else
                {
                    // We're going to try to default to the current executable name
                    // for the source module.  Note that it's possible that we can't
                    // obtain this name in some situations, e.g. when running
                    // on Integration Services Package (SSIS).  In those cases,
                    // this will default to "EasyNetQ".

                    var appPath = Environment.GetCommandLineArgs()[0];

                    if (!string.IsNullOrWhiteSpace(appPath))
                    {
                        sourceModule = Path.GetFileNameWithoutExtension(appPath);
                    }
                }

                var neonLogger      = LogManager.Default.GetLogger(sourceModule);
                var neonLogProvider = new HiveEasyMQLogProvider(neonLogger);

                LogProvider.SetCurrentLogProvider(neonLogProvider);
            }

            if (customServiceAction == null)
            {
                // Use a NOP service action.

                customServiceAction = r => { };
            }

            var bus = RabbitHutch.CreateBus(config, customServiceAction);

            if (!bus.IsConnected)
            {
                bus.Dispose();

                throw new Exception("Cannot to connect to RabbitMQ.");
            }

            return bus;
        }

        /// <summary>
        /// Returns a <see cref="ManagementClient"/> instance that can be used to manage
        /// the RabbitMQ cluster.
        /// </summary>
        /// <param name="username">Optional username (overrides <see cref="Username"/>).</param>
        /// <param name="password">Optional password (overrides <see cref="Password"/>).</param>
        /// <returns>The connected management client.</returns>
        /// <remarks>
        /// <para>
        /// The object returned is thread-safe and most applications should
        /// establish a single connection and then share that for all operations.  
        /// Creating and disposing connections for each operation will be inefficient.
        /// </para>
        /// <note>
        /// The instance returned should be disposed when you are done with it.
        /// </note>
        /// </remarks>
        public ManagementClient ConnectManager(string username = null, string password = null)
        {
            Covenant.Requires<NotImplementedException>(!TlsEnabled, "$todo(jeff.lill): We don't support RabbitMQ TLS yet.");

            var reachableHost = NetHelper.GetReachableHost(AdminHosts);
            var scheme        = TlsEnabled ? "https" : "http";

            username = username ?? Username;
            password = password ?? Password;

            if (string.IsNullOrEmpty(username))
            {
                throw new ArgumentNullException($"[{nameof(username)}] is required.");
            }

            if (string.IsNullOrEmpty(password))
            {
                throw new ArgumentNullException($"[{nameof(password)}] is required.");
            }

            return new ManagementClient($"{scheme}://{reachableHost.Host}", username, password, AdminPort, ssl: TlsEnabled);
        }

        /// <summary>
        /// Returns an <see cref="HiveBus"/> instance that provides more advanced 
        /// capabilites over the very simple <b>EasyNetQ</b> capabilities returned by
        /// <see cref="ConnectEasyNetQ(string, string, string, EasyBusSettings, Action{IServiceRegister})"/>
        /// while still being very easy to use.
        /// </summary>
        /// <param name="username">Optional username (overrides <see cref="Username"/>).</param>
        /// <param name="password">Optional password (overrides <see cref="Password"/>).</param>
        /// <param name="virtualHost">Optional target virtual host (overrides <see cref="VirtualHost"/>).</param>
        /// <param name="settings">Optional message bus client settings.</param>
        /// <returns></returns>
        public HiveBus ConnectHiveBus(
            string          username    = null,
            string          password    = null,
            string          virtualHost = null,
            EasyBusSettings settings    = null)
        {
            username    = username ?? Username;
            password    = password ?? Password;
            virtualHost = virtualHost ?? VirtualHost;

            if (string.IsNullOrEmpty(username))
            {
                throw new ArgumentNullException($"[{nameof(username)}] is required.");
            }

            if (string.IsNullOrEmpty(password))
            {
                throw new ArgumentNullException($"[{nameof(password)}] is required.");
            }

            return new HiveBus(this, username, password, virtualHost);
        }
    }
}
