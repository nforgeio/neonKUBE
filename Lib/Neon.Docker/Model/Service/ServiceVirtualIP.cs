//-----------------------------------------------------------------------------
// FILE:	    ServiceVirtualIP.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Text;

using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Neon.Docker
{
    /// <summary>
    /// Describes the virtual IP address assigned to the service on
    /// a specific attached network.
    /// </summary>
    public class ServiceVirtualIP : INormalizable
    {
        /// <summary>
        /// Specifies the attached network ID.
        /// </summary>
        [JsonProperty(PropertyName = "NetworkID", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(null)]
        public string NetworkID { get; set; }

        /// <summary>
        /// Specifies assigned IP address.
        /// </summary>
        [JsonProperty(PropertyName = "Addr", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(null)]
        public string Addr { get; set; }

        /// <inheritdoc/>
        public void Normalize()
        {
        }
    }
}
