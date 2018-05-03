//-----------------------------------------------------------------------------
// FILE:	    ServiceResources.cs
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
    public class ServiceResources : INormalizable
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public ServiceResources()
        {
        }

        /// <summary>
        /// Specifies resource limits for service containers.
        /// </summary>
        [JsonProperty(PropertyName = "Limits", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(null)]
        public ServiceResourceSettings Limits { get; set; }

        /// <summary>
        /// Specifies resource reservations for service containers.
        /// </summary>
        [JsonProperty(PropertyName = "Reservation", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(null)]
        public ServiceResourceSettings Reservation { get; set; }

        /// <inheritdoc/>
        public void Normalize()
        {
            Limits      = Limits ?? new ServiceResourceSettings();
            Reservation = Reservation ?? new ServiceResourceSettings();
        }
    }
}
