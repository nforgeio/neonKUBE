//-----------------------------------------------------------------------------
// FILE:	    KubernetesOptions.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using YamlDotNet.Serialization;

using Neon.Common;
using Neon.Net;

namespace Neon.Kube
{
    /// <summary>
    /// Describes the Kubernetes options for a neonKUBE.
    /// </summary>
    public class KubernetesOptions
    {
        private const string minVersion              = "1.13.0";
        private const string defaultVersion          = "default";
        private const string defaultDashboardVersion = "default";

        /// <summary>
        /// Default constructor.
        /// </summary>
        public KubernetesOptions()
        {
        }

        /// <summary>
        /// The version of Kubernetes to be installed.  This defaults to <b>default</b> which
        /// will install the latest tested version of Kubernetes.  The minimum supported
        /// version is <b>1.13.0</b>.
        /// </summary>
        [JsonProperty(PropertyName = "Version", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "Version", ApplyNamingConventions = false)]
        [DefaultValue(defaultVersion)]
        public string Version { get; set; } = defaultVersion;

        /// <summary>
        /// The version of Kubernetes dashboard to be installed.  This defaults to <b>default</b> which
        /// will install the latest tested version of Kubernetes.
        /// </summary>
        [JsonProperty(PropertyName = "DashboardVersion", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "DashboardVersion", ApplyNamingConventions = false)]
        [DefaultValue(defaultVersion)]
        public string DashboardVersion { get; set; } = defaultDashboardVersion;

        /// <summary>
        /// The version of Istio to be installed.  This defaults to <b>default</b> which
        /// will install a reasonable version for the Kubernetes release being inbstalled.
        /// </summary>
        [JsonProperty(PropertyName = "IstioVersion", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "IstioVersion", ApplyNamingConventions = false)]
        [DefaultValue("default")]
        public string IstioVersion { get; set; } = "default";

        /// <summary>
        /// Validates the options and also ensures that all <c>null</c> properties are
        /// initialized to their default values.
        /// </summary>
        /// <param name="clusterDefinition">The cluster definition.</param>
        /// <exception cref="ClusterDefinitionException">Thrown if the definition is not valid.</exception>
        [Pure]
        public void Validate(ClusterDefinition clusterDefinition)
        {
            Covenant.Requires<ArgumentNullException>(clusterDefinition != null);

            Version = Version ?? defaultVersion;
            Version = Version.ToLowerInvariant();

            if (Version != defaultVersion)
            {
                if (!System.Version.TryParse(Version, out var vKubernetes))
                {
                    throw new ClusterDefinitionException($"[{nameof(KubernetesOptions)}.{nameof(Version)}={Version}] is not a valid Kubernetes version.");
                }

                if (vKubernetes < System.Version.Parse(minVersion))
                {
                    throw new ClusterDefinitionException($"[{nameof(KubernetesOptions)}.{nameof(Version)}={Version}] is less than the supported version [{minVersion}].");
                }
            }

            if (DashboardVersion != defaultDashboardVersion)
            {
                if (!System.Version.TryParse(DashboardVersion, out var vDashboard))
                {
                    throw new ClusterDefinitionException($"[{nameof(KubernetesOptions)}.{nameof(DashboardVersion)}={DashboardVersion}] is not a valid version number.");
                }
            }

            if (IstioVersion != "default" && !System.Version.TryParse(IstioVersion, out var vIstio))
            {
                throw new ClusterDefinitionException($"[{nameof(KubernetesOptions)}.{nameof(IstioVersion)}={IstioVersion}] is invalid].");
            }
        }

        /// <summary>
        /// Clears any sensitive properties like the Docker registry credentials.
        /// </summary>
        public void ClearSecrets()
        {
        }
    }
}
