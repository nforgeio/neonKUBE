//-----------------------------------------------------------------------------
// FILE:	    ServiceEndpointPortConfig.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Text;

using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using YamlDotNet.Serialization;

namespace Neon.Docker
{
    /// <summary>
    /// Service port publication specification.
    /// </summary>
    public class ServiceEndpointPortConfig : INormalizable
    {
        /// <summary>
        /// The port name.
        /// </summary>
        [JsonProperty(PropertyName = "Name", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [YamlMember(Alias = "Name")]
        [DefaultValue(null)]
        public string Name { get; set; }

        /// <summary>
        /// Specifies the port protocol.
        /// </summary>
        [JsonProperty(PropertyName = "Protocol", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [YamlMember(Alias = "Protocol")]
        [DefaultValue(default(ServicePortProtocol))]
        public ServicePortProtocol Protocol { get; set; }

        /// <summary>
        /// Specifies the internal port where external traffic
        /// will be forwarded within the service containers.
        /// </summary>
        [JsonProperty(PropertyName = "TargetPort", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [YamlMember(Alias = "TargetPort")]
        [DefaultValue(null)]
        public int TargetPort { get; set; }

        /// <summary>
        /// Specifies the port where the service receives traffic on the
        /// external network.
        /// </summary>
        [JsonProperty(PropertyName = "PublishedPort", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [YamlMember(Alias = "PublishedPort")]
        [DefaultValue(null)]
        public int PublishedPort { get; set; }

        /// <summary>
        /// Specifies the port mode.
        /// </summary>
        [JsonProperty(PropertyName = "PublishMode", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [YamlMember(Alias = "PublishMode")]
        [DefaultValue(default(ServicePortMode))]
        public ServicePortMode PublishMode { get; set; }

        /// <inheritdoc/>
        public void Normalize()
        {
        }
    }
}
