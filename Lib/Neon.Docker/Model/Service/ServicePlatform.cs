//-----------------------------------------------------------------------------
// FILE:	    ServicePlatform.cs
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
    /// Service container spread placement settings.
    /// </summary>
    public class ServicePlatform : INormalizable
    {
        /// <summary>
        /// Specifies the hardware architecture (like: <b>x86_64</b>).
        /// </summary>
        [JsonProperty(PropertyName = "Architecture", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [YamlMember(Alias = "Architecture", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string Architecture { get; set; }

        /// <summary>
        /// Specifies the operating system (like: <b>linux</b> or <b>windows</b>).
        /// </summary>
        [JsonProperty(PropertyName = "OS", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [YamlMember(Alias = "OS", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string OS { get; set; }

        /// <inheritdoc/>
        public void Normalize()
        {
        }
    }
}
