//-----------------------------------------------------------------------------
// FILE:	    RabbitMQSettings.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;

using Neon.Common;
using Neon.Net;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Neon.Data
{
    /// <summary>
    /// Settings used to connect a RabbitMQ client to a broker.
    /// </summary>
    public class RabbitMQSettings
    {
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
        /// connection problems if any single node is down.
        /// </remarks>
        [JsonProperty(PropertyName = "Hostnames", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public List<string> Hostnames { get; set; } = new List<string>();

        /// <summary>
        /// Specifies the broker port number.  This defaults to <b>5672</b>.  You
        /// should use <b>5671</b> if TLS is enabled on the broker.
        /// </summary>
        [JsonProperty(PropertyName = "Port", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(0)]
        public int Port { get; set; } = 5672;

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

                if (Hostnames == null || Hostnames.Count == 0)
                {
                    return false;
                }

                foreach (var hostname in Hostnames)
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
