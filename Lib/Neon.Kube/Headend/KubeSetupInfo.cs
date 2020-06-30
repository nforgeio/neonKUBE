//-----------------------------------------------------------------------------
// FILE:	    KubeSetupInfo.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2020 by neonFORGE, LLC.  All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

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
using YamlDotNet.Core;
using YamlDotNet.Serialization;

using Neon.Common;
using Neon.IO;
using Neon.Net;
using Neon.Retry;
using Neon.Time;

namespace Neon.Kube
{
    /// <summary>
    /// Describes the details required to setup a Kubernetes cluster.
    /// </summary>
    public class KubeSetupInfo
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public KubeSetupInfo()
        {
        }

        //---------------------------------------------------------------------
        // Cluster prepare details.

        /// <summary>
        /// Returns the URI for the Linux node template for the specified hosting enviroment 
        /// and Linux distribution and version.  This may be null for some hosting environments
        /// like bare metal and clouds.
        /// </summary>
        [JsonProperty(PropertyName = "LinuxTemplateUri", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "linuxTemplateUri", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string LinuxTemplateUri { get; set; } = null;

        //---------------------------------------------------------------------
        // Component versions.

        /// <summary>
        /// Lists the installed component versions.
        /// </summary>
        [JsonProperty(PropertyName = "Versions", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "versions", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public KubeSetupVersions Versions { get; set; } = new KubeSetupVersions();

        //---------------------------------------------------------------------
        // kubectl:

        /// <summary>
        /// The <b>kubectl</b> binary download URI for Linux.
        /// </summary>
        [JsonProperty(PropertyName = "KubeCtlLinuxUri", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "kubeCtlLinuxUri", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string KubeCtlLinuxUri { get; set; }

        /// <summary>
        /// The <b>kubectl</b> binary download URI for OS/X.
        /// </summary>
        [JsonProperty(PropertyName = "KubeCtlOsxUri", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "kubeCtlOsxUri", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string KubeCtlOsxUri { get; set; }

        /// <summary>
        /// The <b>kubectl</b> binary download URI for Windows.
        /// </summary>
        [JsonProperty(PropertyName = "KubeCtlWindowsUri", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "kubeCtlWindowsUri", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string KubeCtlWindowsUri { get; set; }

        //---------------------------------------------------------------------
        // kubeadm:

        /// <summary>
        /// The <b>kubeadm</b> binary download URI for Linux.
        /// </summary>
        [JsonProperty(PropertyName = "KubeAdmLinuxUri", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "kubeAdmLinuxUri", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string KubeAdmLinuxUri { get; set; }

        /// <summary>
        /// The <b>kubelet</b> binary download URI for Linux.
        /// </summary>
        [JsonProperty(PropertyName = "KubeletLinuxUri", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "kubeletLinuxUri", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string KubeletLinuxUri { get; set; }

        //---------------------------------------------------------------------
        // Ubuntu Kubernetes component package versions:

        /// <summary>
        /// The Ubuntu package version for <b>kubeadm</b>.
        /// </summary>
        [JsonProperty(PropertyName = "KubeAdmPackageUbuntuVersion", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "kubeAdmPackageUbuntuVersion", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string KubeAdmPackageUbuntuVersion { get; set; }

        /// <summary>
        /// The Ubuntu package version for <b>kubectl</b>.
        /// </summary>
        [JsonProperty(PropertyName = "KubeCtlPackageUbuntuVersion", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "kubeCtlPackageUbuntuVersion", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string KubeCtlPackageUbuntuVersion { get; set; }

        /// <summary>
        /// The Ubuntu package version for <b>kubelet</b>.
        /// </summary>
        [JsonProperty(PropertyName = "KubeletPackageUbuntuVersion", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "kubeletPackageUbuntuVersion", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string KubeletPackageUbuntuVersion { get; set; }

        //---------------------------------------------------------------------
        // Docker:

        /// <summary>
        /// The Docker package for Ubuntu.
        /// </summary>
        [JsonProperty(PropertyName = "DockerPackageUbuntuUri", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "dockerPackageUbuntuUri", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string DockerPackageUbuntuUri { get; set; }

        //---------------------------------------------------------------------
        // Helm:

        /// <summary>
        /// The Helm binary URL for Linux.
        /// </summary>
        [JsonProperty(PropertyName = "HelmLinuxUri", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "helmLinuxUri", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string HelmLinuxUri { get; set; }

        /// <summary>
        /// The Helm binary URL for OS/X.
        /// </summary>
        [JsonProperty(PropertyName = "HelmOsxUri", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "helmOsxUri", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string HelmOsxUri { get; set; }

        /// <summary>
        /// The Helm binary URL for Windows.
        /// </summary>
        [JsonProperty(PropertyName = "HelmWindowsUri", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "helmWindowsUri", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string HelmWindowsUri { get; set; }

        //---------------------------------------------------------------------
        // Calico:

        /// <summary>
        /// The Calico RBAC rules download (YAML for kubectl).
        /// </summary>
        [JsonProperty(PropertyName = "CalicoRbacYamlUri", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "calicoRbacYamlUri", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string CalicoRbacYamlUri { get; set; }

        /// <summary>
        /// The Calico setup download (YAML for kubectl).
        /// </summary>
        [JsonProperty(PropertyName = "CalicoSetupYamlUri", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "calicoSetupYamlUri", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string CalicoSetupYamlUri { get; set; }

        //---------------------------------------------------------------------
        // Istio:

        /// <summary>
        /// The Istio binary URL for Linux.
        /// </summary>
        [JsonProperty(PropertyName = "IstioLinuxUri", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "istioLinuxUri", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string IstioLinuxUri { get; set; }

        //---------------------------------------------------------------------
        // Kubernetes Dashboard:

        /// <summary>
        /// <para>
        /// The Kubernetes Dashboard configuration YAML.
        /// </para>
        /// <note>
        /// See the remarks for information on embedded variables.
        /// </note>
        /// </summary>
        /// <remarks>
        /// The YAML returned includes two variables <b>$&lt;CERTIFICATE&gt;</b> and <b>$&lt;PRIVATEKEY&gt;</b> that 
        /// need to be replaced with the custom certificate and private key the dashboard will use to
        /// secure the site.  These must be passed as the PEM encoded certificate and key converted into
        /// a single line of base-64 text.
        /// </remarks>
        [JsonProperty(PropertyName = "KubeDashboardYaml", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "KubeDashboardYaml", ScalarStyle = ScalarStyle.Literal, ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string KubeDashboardYaml { get; set; }
    }
}
