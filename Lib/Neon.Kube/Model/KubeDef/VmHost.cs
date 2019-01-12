//-----------------------------------------------------------------------------
// FILE:	    VmHost.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.

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

namespace Neon.Kube
{
    /// <summary>
    /// Describes the location and credentials required to connect to
    /// a specific Hyper-V or XenServer hypervisor instance for cluster 
    /// provisioning.
    /// </summary>
    public class VmHost
    {
        /// <summary>
        /// The XenServer hostname.  This is used to by <see cref="NodeDefinition"/> instances
        /// to specify where a cluster node is to be provisioned.
        /// </summary>
        [JsonProperty(PropertyName = "Name", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string Name { get; set; }

        /// <summary>
        /// The IP address or FQDN of the hypervisor machine.
        /// </summary>
        [JsonProperty(PropertyName = "Address", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
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

        /// <summary>
        /// Validates the options.
        /// </summary>
        /// <param name="clusterDefinition">The cluster definition.</param>
        /// <exception cref="KubeDefinitionException">Thrown if the definition is not valid.</exception>
        [Pure]
        internal void Validate(KubeDefinition clusterDefinition)
        {
            if (string.IsNullOrEmpty(Name))
            {
                throw new KubeDefinitionException($"[{nameof(VmHost)}.{nameof(Name)}] is required when specifying a hypervisor host.");
            }

            if (string.IsNullOrEmpty(Address))
            {
                throw new KubeDefinitionException($"[{nameof(VmHost)}.{nameof(Address)}] is required when specifying a hypervisor host.");
            }

            if (string.IsNullOrEmpty(Username) && string.IsNullOrEmpty(clusterDefinition.Hosting.VmHostUsername))
            {
                throw new KubeDefinitionException($"[{nameof(VmHost)}.{nameof(Username)}] is required when specifying a hypervisor host and no default username is specified by [{nameof(HostingOptions)}.{nameof(HostingOptions.VmHostUsername)}].");
            }

            if (string.IsNullOrEmpty(Password) && string.IsNullOrEmpty(clusterDefinition.Hosting.VmHostPassword))
            {
                throw new KubeDefinitionException($"[{nameof(VmHost)}.{nameof(Password)}] is required when specifying a hypervisor host and no default password is specified by [{nameof(HostingOptions)}.{nameof(HostingOptions.VmHostPassword)}].");
            }
        }
    }
}
