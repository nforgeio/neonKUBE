//-----------------------------------------------------------------------------
// FILE:	    ServiceDnsConfig.cs
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
    /// Specifies service container DNS related options.
    /// </summary>
    public class ServiceDnsConfig : INormalizable
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public ServiceDnsConfig()
        {
        }

        /// <summary>
        /// IP addresses of the nameservers.
        /// </summary>
        [JsonProperty(PropertyName = "Nameservers", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(null)]
        public List<string> Nameservers { get; set; }

        /// <summary>
        /// Domain search list for host name lookups.
        /// </summary>
        [JsonProperty(PropertyName = "Search", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(null)]
        public List<string> Search { get; set; }
        
        /// <summary>
        /// Low-level internal resolver options.
        /// </summary>
        [JsonProperty(PropertyName = "Options", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(null)]
        public List<string> Options { get; set; }

        /// <inheritdoc/>
        public void Normalize()
        {
            Nameservers = Nameservers ?? new List<string>();
            Search      = Search ?? new List<string>();
            Options     = Options ?? new List<string>();
        }
    }
}
