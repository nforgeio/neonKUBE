//-----------------------------------------------------------------------------
// FILE:	    HostingOptions.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2017 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

using Neon.Common;
using Neon.Net;

namespace Neon.Cluster
{
    /// <summary>
    /// Specifies the cloud or colocation/on-premise hosting settings.
    /// </summary>
    public class HostingOptions
    {
        /// <summary>
        /// Default constructor that initializes a <see cref="HostingEnvironments.Machine"/> provider.
        /// </summary>
        public HostingOptions()
        {
        }

        /// <summary>
        /// Identifies the cloud or other hosting platform.  This defaults to <see cref="HostingEnvironments.Machine"/>.
        /// </summary>
        [JsonProperty(PropertyName = "Environment", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(HostingEnvironments.Machine)]
        public HostingEnvironments Environment { get; set; } = HostingEnvironments.Machine;

        /// <summary>
        /// Specifies the Amazon Web Services hosting settings.
        /// </summary>
        [JsonProperty(PropertyName = "Aws", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public AwsOptions Aws { get; set; } = null;

        /// <summary>
        /// Specifies the Microsoft Azure hosting settings.
        /// </summary>
        [JsonProperty(PropertyName = "Azure", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public AzureOptions Azure { get; set; } = null;

        /// <summary>
        /// Specifies the Google Cloud Platform hosting settings.
        /// </summary>
        [JsonProperty(PropertyName = "Google", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public GoogleOptions Google { get; set; } = null;

        /// <summary>
        /// Specifies the hosting settings when hosting directly on bare metal or virtual machines.
        /// </summary>
        [JsonProperty(PropertyName = "Machine", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public MachineOptions Machine { get; set; } = null;

        /// <summary>
        /// Specifies the hosting settings when hosting on Microsoft Hyper-V virtual machines.
        /// </summary>
        [JsonProperty(PropertyName = "HyperV", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public HyperVOptions HyperV { get; set; } = null;

        /// <summary>
        /// Returns <c>true</c> if the cluster will be hosted by a cloud provider like AWS or Azure.
        /// </summary>
        [JsonIgnore]
        public bool IsCloudProvider
        {
            get
            {
                switch (Environment)
                {
                    case HostingEnvironments.HyperV:
                    case HostingEnvironments.Machine:

                        return false;

                    case HostingEnvironments.Aws:
                    case HostingEnvironments.Azure:
                    case HostingEnvironments.Google:

                        return true;

                    default:

                        throw new NotImplementedException("Unexpected hosting environment.");
                }
            }
        }

        /// <summary>
        /// Validates the options definition and also ensures that all <c>null</c> properties are
        /// initialized to their default values.
        /// </summary>
        /// <param name="clusterDefinition">The cluster definition.</param>
        /// <exception cref="ClusterDefinitionException">Thrown if the definition is not valid.</exception>
        [Pure]
        public void Validate(ClusterDefinition clusterDefinition)
        {
            Covenant.Requires<ArgumentNullException>(clusterDefinition != null);

            switch (Environment)
            {
                case HostingEnvironments.Aws:

                    if (Aws == null)
                    {
                        throw new ClusterDefinitionException($"[{nameof(HostingOptions)}.{nameof(Aws)}] must be initialized when cloud provider is [{Environment}].");
                    }

                    Aws.Validate(clusterDefinition);
                    break;

                case HostingEnvironments.Azure:

                    if (Azure == null)
                    {
                        throw new ClusterDefinitionException($"[{nameof(HostingOptions)}.{nameof(Azure)}] must be initialized when cloud provider is [{Environment}].");
                    }

                    Azure.Validate(clusterDefinition);
                    break;

                case HostingEnvironments.Google:

                    if (Google == null)
                    {
                        throw new ClusterDefinitionException($"[{nameof(HostingOptions)}.{nameof(Google)}] must be initialized when cloud provider is [{Environment}].");
                    }

                    Google.Validate(clusterDefinition);
                    break;

                case HostingEnvironments.HyperV:

                    HyperV = HyperV ?? new HyperVOptions();

                    HyperV.Validate(clusterDefinition);
                    break;

                case HostingEnvironments.Machine:

                    Machine = Machine ?? new MachineOptions();

                    Machine.Validate(clusterDefinition);
                    break;

                default:

                    throw new NotImplementedException();
            }

            if (IsCloudProvider && !clusterDefinition.Vpn.Enabled)
            {
                // VPN is implicitly enabled when hosting on a cloud.

                clusterDefinition.Vpn.Enabled = true;
            }
        }

        /// <summary>
        /// Clears all hosting provider details because they may
        /// include hosting related secrets.
        /// </summary>
        public void ClearSecrets()
        {
            Aws     = null;
            Azure   = null;
            Google  = null;
            Machine = null;
        }
    }
}
