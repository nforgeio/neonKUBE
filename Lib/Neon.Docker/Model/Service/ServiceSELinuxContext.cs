//-----------------------------------------------------------------------------
// FILE:	    ServiceSELinuxContext.cs
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
    /// SELinux labels for the container.
    /// </summary>
    public class ServiceSELinuxContext : INormalizable
    {
        /// <summary>
        /// Disable SELinux.
        /// </summary>
        [JsonProperty(PropertyName = "Disable", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(false)]
        public bool Disable { get; set; }

        /// <summary>
        /// SELinux user label.
        /// </summary>
        [JsonProperty(PropertyName = "User", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(null)]
        public string User { get; set; }

        /// <summary>
        /// SELinux role label.
        /// </summary>
        [JsonProperty(PropertyName = "Role", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(null)]
        public string Role { get; set; }

        /// <summary>
        /// SELinux type label.
        /// </summary>
        [JsonProperty(PropertyName = "Type", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(null)]
        public string Type { get; set; }

        /// <summary>
        /// SELinux level label.
        /// </summary>
        [JsonProperty(PropertyName = "Level", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(null)]
        public string Level { get; set; }

        /// <inheritdoc/>
        public void Normalize()
        {
        }
    }
}
