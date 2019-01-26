//-----------------------------------------------------------------------------
// FILE:	    KubeSetupVersions.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using YamlDotNet.Serialization;

using Neon.Common;
using Neon.IO;
using Neon.Net;
using Neon.Retry;
using Neon.Time;

namespace Neon.Kube
{
    /// <summary>
    /// Indicates the versions for the installed components.
    /// </summary>
    public class KubeSetupVersions
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public KubeSetupVersions()
        {
        }

        /// <summary>
        /// The Kubernetes version.
        /// </summary>
        [JsonProperty(PropertyName = "Kubernetes", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "Kubernetes", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string Kubernetes { get; set; }

        /// <summary>
        /// The Kubernetes dashboard version;
        /// </summary>
        [JsonProperty(PropertyName = "KubernetesDashboard", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "KubernetesDashboard", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string KubernetesDashboard { get; set; }

        /// <summary>
        /// The Docker version.
        /// </summary>
        [JsonProperty(PropertyName = "DockerVersion", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "DockerVersion", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string Docker { get; set; }

        /// <summary>
        /// The Helm version.
        /// </summary>
        [JsonProperty(PropertyName = "Helm", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "Helm", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string Helm { get; set; }

        /// <summary>
        /// The Istio version.
        /// </summary>
        [JsonProperty(PropertyName = "Istio", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "Istio", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string Istio { get; set; }
    }
}
