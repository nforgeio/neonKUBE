//-----------------------------------------------------------------------------
// FILE:	    VmHost.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2017 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

using Neon.Common;
using Neon.IO;

namespace Neon.Cluster
{
    /// <summary>
    /// Describes the location and credentials required to connect to
    /// a specific Hyper-V or XenServer hypervisor instance for cluster 
    /// provisioning.
    /// </summary>
    public class VmHost
    {
        /// <summary>
        /// The IP address or FQDN of the hypervisor machine.
        /// </summary>
        public string Address { get; set; }

        /// <summary>
        /// The custom username to use when connecting to the hypervisor machine.  This
        /// overrides <see cref="HostingOptions.VmHostUsername"/>.
        /// </summary>
        [JsonProperty(PropertyName = "Username", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string Username { get; set; }

        /// <summary>
        /// The custom password to use when connecting to the hypervisor machine.  This
        /// overrides <see cref="HostingOptions.VmHostPassword"/>.
        /// </summary>
        [JsonProperty(PropertyName = "Password", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string Password { get; set; }
    }
}
