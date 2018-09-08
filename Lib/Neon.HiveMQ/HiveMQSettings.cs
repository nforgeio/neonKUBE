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
        /// Specifies the virtual host namespace.  This defaults to <b>"/"</b>.
        /// </summary>
        [JsonProperty(PropertyName = "VirtualHost", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string VirtualHost { get; set; } = "/";

        /// <summary>
        /// Specifies the broker's hostname or IP address.
        /// </summary>
        /// <remarks>
        /// You must specify the hostname/address for at least one operating RabbitMQ node.  
        /// The RabbitMQ client will use this to discover the remaining nodes.  It is a best 
        /// practice to specify multiple nodes in a clustered environment to avoid initial
        /// connection problems when any single node is down.
        /// </remarks>
        [JsonProperty(PropertyName = "Hosts", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public List<string> Hosts { get; set; } = new List<string>();

        /// <summary>
        /// Specifies the broker port number.  This defaults to <b>5672</b> (the non-TLS port).
        /// </summary>
        [JsonProperty(PropertyName = "Port", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(NetworkPorts.AMQP)]
        public int Port { get; set; } = NetworkPorts.AMQP;

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
        public bool IsValid
        {
            get
            {
                if (string.IsNullOrEmpty(VirtualHost))
                {
                    return false;
                }

                if (Hosts == null || Hosts.Count == 0)
                {
                    return false;
                }

                foreach (var hostname in Hosts)
                {
                    if (string.IsNullOrEmpty(hostname))
                    {
                        return false;
                    }
                }

                return NetHelper.IsValidPort(Port);
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
        public IConnection Connect(string username = null, string password = null, bool dispatchConsumersAsync = false)
        {
            Covenant.Requires<ArgumentNullException>(Hosts != null && Hosts.Count > 0);

            var connectionFactory = new ConnectionFactory();

            connectionFactory.VirtualHost            = VirtualHost;
            connectionFactory.UserName               = username ?? Username;
            connectionFactory.Password               = password ?? Password;
            connectionFactory.Port                   = Port;
            connectionFactory.DispatchConsumersAsync = dispatchConsumersAsync;

            if (TlsEnabled)
            {
                connectionFactory.Ssl = new SslOption() { Enabled = true };
            }

            return new HiveMQConnection(connectionFactory.CreateConnection(Hosts));
        }

        /// <summary>
        /// Returns a RabbitMQ cluster connection using specified settings and credentials.
        /// </summary>
        /// <param name="credentials">The credentials.</param>
        /// <param name="dispatchConsumersAsync">Optionally enables <c>async</c> message consumers.  This defaults to <c>false</c>.</param>
        /// <returns>The RabbitMQ <see cref="IConnection"/>.</returns>
        public IConnection Connect(Credentials credentials, bool dispatchConsumersAsync = false)
        {
            Covenant.Requires<ArgumentNullException>(credentials != null);
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(VirtualHost));
            Covenant.Requires<ArgumentNullException>(Hosts != null && Hosts.Count > 0);

            return Connect(credentials.Username, credentials.Password, dispatchConsumersAsync);
        }

        /// <summary>
        /// Returns an EasyNetQ <see cref="IBus"/> connection to a RabbitMQ cluster.
        /// </summary>
        /// <param name="username">Optional username.</param>
        /// <param name="password">Optional password.</param>
        /// <param name="settings">Optional message bus client settings.</param>
        /// <param name="customServiceAction">
        /// Optionally specifies an action that overrides the default the EasyNetQ
        /// client  configuration via dependency injection.
        /// </param>
        /// <returns>The connected <see cref="IBus"/>.</returns>
        public IBus ConnectBus(string username = null, string password = null, BusSettings settings = null, Action<IServiceRegister> customServiceAction = null)
        {
            Covenant.Requires<NotImplementedException>(!TlsEnabled, "$todo(jeff.lill): We don't support RabbitMQ TLS yet.");

            var config = new ConnectionConfiguration()
            {
                Port     = (ushort)Port,
                UserName = username ?? Username,
                Password = password ?? Password
            };

            if (settings != null)
            {
                settings.ApplyTo(config);
            }

            // Enable Neon based logging if requested (which is the default).

            if (NeonLog)
            {
                // Generate a reasonable [sourceModule] setting.

                var sourceModule = "EasyNetQ";
                var product      = settings?.Product ?? string.Empty;
                var app          = settings?.Name ?? string.Empty;

                if (!string.IsNullOrEmpty(product) || !string.IsNullOrEmpty(app))
                {
                    if (string.IsNullOrEmpty(product))
                    {
                        sourceModule = app;
                    }
                    else if (string.IsNullOrEmpty(app))
                    {
                        sourceModule = product;
                    }
                    else
                    {
                        sourceModule = $"{product}/{app}";
                    }
                }
                else
                {
                    // We're going to try to default to the current executable name
                    // for the source module.  Note that it's possible that we can't
                    // obtain this name in some situations, e.g. when running
                    // on Integration Services Package (SSIS).  In those cases,
                    // we'll default to "EasyNetQ".

                    var appPath = Environment.GetCommandLineArgs()[0];

                    if (!string.IsNullOrWhiteSpace(appPath))
                    {
                        sourceModule = Path.GetFileName(appPath);
                    }
                }

                var neonLogger      = LogManager.Default.GetLogger(sourceModule);
                var neonLogProvider = new NeonLoggingProvider(neonLogger);

                LogProvider.SetCurrentLogProvider(neonLogProvider);
            }

            return RabbitHutch.CreateBus(config, customServiceAction);
        }

        /// <summary>
        /// Returns a <see cref="ManagementClient"/> instance that can be used to manage
        /// the RabbitMQ cluster.
        /// </summary>
        /// <returns>The connected management client.</returns>
        public ManagementClient Manager()
        {
            // $todo(jeff.lill): Implement this.

            return null;
        }
    }
}
