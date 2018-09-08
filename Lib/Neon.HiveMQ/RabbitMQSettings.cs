//-----------------------------------------------------------------------------
// FILE:	    RabbitMQSettings.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Contracts;

using Neon.Common;
using Neon.Net;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Neon.HiveMQ
{
    /// <summary>
    /// Settings used to connect a RabbitMQ client to a message broker.
    /// </summary>
    public class RabbitMQSettings
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Parses a <see cref="RabbitMQSettings"/> from a JSON or YAML string,
        /// automatically detecting the input format.
        /// </summary>
        /// <param name="jsonOrYaml"></param>
        /// <param name="strict">Optionally require that all input properties map to route properties.</param>
        /// <returns>The parsed <see cref="RabbitMQSettings"/>.</returns>
        public static RabbitMQSettings Parse(string jsonOrYaml, bool strict = false)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(jsonOrYaml));

            return NeonHelper.JsonOrYamlDeserialize<RabbitMQSettings>(jsonOrYaml, strict);
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
    }
}
