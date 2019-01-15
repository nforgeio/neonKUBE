//-----------------------------------------------------------------------------
// FILE:	    ServiceResources.cs
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
    /// Specifies the service resource requirements and limits.
    /// </summary>
    public class ServiceResources : INormalizable
    {
        /// <summary>
        /// Specifies resource limits for service containers.
        /// </summary>
        [JsonProperty(PropertyName = "Limits", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [YamlMember(Alias = "Limits", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public ServiceResourceSettings Limits { get; set; }

        /// <summary>
        /// Specifies resource reservations for service containers.
        /// </summary>
        [JsonProperty(PropertyName = "Reservations", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [YamlMember(Alias = "Reservations", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public ServiceResourceSettings Reservations { get; set; }

        /// <inheritdoc/>
        public void Normalize()
        {
            Limits       = Limits ?? new ServiceResourceSettings();
            Reservations = Reservations ?? new ServiceResourceSettings();

            Limits?.Normalize();
            Reservations?.Normalize();
        }
    }
}
