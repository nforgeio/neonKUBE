//-----------------------------------------------------------------------------
// FILE:	    RabbitMQSettings.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2017 by NeonForge, LLC.  All rights reserved.

using System;
using System.Collections.Generic;

using Neon.Common;
using Neon.Data;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Neon.Cluster
{
    /// <summary>
    /// Settings used to connect a RabbitMQ client to a broker.  This class sets
    /// <see cref="IEntity.Type"/> to <see cref="NeonEntityTypes.RabbitMQSettings"/>.
    /// </summary>
    public class RabbitMQSettings : Entity
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        public RabbitMQSettings()
        {
            Type = NeonEntityTypes.RabbitMQSettings;
        }

        /// <summary>
        /// Specifies the virtual host namespace.  This defaults to <b>"/"</b>.
        /// </summary>
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
        public List<string> Hostnames { get; set; } = new List<string>();

        /// <summary>
        /// Specifies the broker port number.  This defaults to <b>5672</b>.  You
        /// should use <b>5671</b> if TLS is enabled on the broker.
        /// </summary>
        public int Port { get; set; } = 5672;
    }
}
