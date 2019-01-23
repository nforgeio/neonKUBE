//-----------------------------------------------------------------------------
// FILE:	    KubeSetupInfo.cs
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
    /// Holds information required to setup a Kubernetes cluster.
    /// </summary>
    public class KubeSetupInfo
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public KubeSetupInfo()
        {
        }

        /// <summary>
        /// Lists the installed component versions.
        /// </summary>
        [JsonProperty(PropertyName = "Versions", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "Versions", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public KubeSetupVersions Versions { get; set; } = new KubeSetupVersions();

        /// <summary>
        /// The <b>kubectl</b> binary download URI for Windows.
        /// </summary>
        [JsonProperty(PropertyName = "OsxKubeAdminUri", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "OsxKubeAdminUri", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string WindowsKubeCtlUri { get; set; }

        /// <summary>
        /// The <b>kubectl</b> binary download URI for OS/X.
        /// </summary>
        [JsonProperty(PropertyName = "OsxKubeCtlUri", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "OsxKubeCtlUri", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string OsxKubeCtlUri { get; set; }

        /// <summary>
        /// The <b>kubeadm</b> binary download URI for Linux.
        /// </summary>
        [JsonProperty(PropertyName = "LinuxKubeAdminUri", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "LinuxKubeAdminUri", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string LinuxKubeAdminUri { get; set; }

        /// <summary>
        /// The <b>kubectl</b> binary download URI for Linux.
        /// </summary>
        [JsonProperty(PropertyName = "LinuxKubeCtlUri", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "LinuxKubeCtlUri", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string LinuxKubeCtlUri { get; set; }

        /// <summary>
        /// The <b>kubelet</b> binary download URI for Linux.
        /// </summary>
        [JsonProperty(PropertyName = "LinuxKubeletUri", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "LinuxKubeletUri", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string LinuxKubeletUri { get; set; }

        /// <summary>
        /// The Ubuntu package version for <b>kubeadm.</b>.
        /// </summary>
        [JsonProperty(PropertyName = "UbuntuKubeAdmPackageVersion", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "UbuntuKubeAdmPackageVersion", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string UbuntuKubeAdmPackageVersion { get; set; }

        /// <summary>
        /// The Ubuntu package version for <b>kubectl.</b>.
        /// </summary>
        [JsonProperty(PropertyName = "UbuntuKubeCtlPackageVersion", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "UbuntuKubeCtlPackageVersion", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string UbuntuKubeCtlPackageVersion { get; set; }

        /// <summary>
        /// The Ubuntu package version for <b>kubelet.</b>.
        /// </summary>
        [JsonProperty(PropertyName = "UbuntuKubeletPackageVersion", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "UbuntuKubeletPackageVersion", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string UbuntuKubeletPackageVersion { get; set; }

        /// <summary>
        /// The Docker package for Ubuntu.
        /// </summary>
        [JsonProperty(PropertyName = "UbuntuDockerPackageUri", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "UbuntuDockerPackageUri", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string UbuntuDockerPackageUri { get; set; }

        /// <summary>
        /// The Istio binary URL for Linux.
        /// </summary>
        [JsonProperty(PropertyName = "IstioLinuxUri", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "IstioLinuxUri", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string IstioLinuxUri { get; set; }

        /// <summary>
        /// The Kubernetes Dashboard resource configuration URI.
        /// </summary>
        [JsonProperty(PropertyName = "KubeDashboardUri", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "KubeDashboardUri", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string KubeDashboardUri { get; set; }
    }
}
