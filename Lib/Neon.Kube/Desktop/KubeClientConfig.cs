//-----------------------------------------------------------------------------
// FILE:	    KubeClientConfig.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2020 by neonFORGE, LLC.  All rights reserved.
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
using YamlDotNet.Serialization;

using Neon.Common;
using Neon.IO;
using Neon.Net;

namespace Neon.Kube
{
    /// <summary>
    /// Used by the neonKUBE client desktop and command line tools to 
    /// manage the client side configuration.
    /// </summary>
    public class KubeClientConfig
    {
        /// <summary>
        /// Default constuctor.
        /// </summary>
        public KubeClientConfig()
        {
        }

        /// <summary>
        /// The schema version for this state file.
        /// </summary>
        [JsonProperty(PropertyName = "Schema", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "schema", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string Schema { get; set; } = "v1";

        /// <summary>
        /// The globally unique client installation ID.
        /// </summary>
        [JsonProperty(PropertyName = "InstallationId", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "installationId", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string InstallationId { get; set; }

        /// <summary>
        /// The interval the desktop application uses to poll for changes to the Kubernetes
        /// cluster configuration state.  This defaults to <b>1 second</b>.
        /// </summary>
        [JsonProperty(PropertyName = "StatusPollSeconds", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "statusPollSeconds", ApplyNamingConventions = false)]
        [DefaultValue(1)]
        public int StatusPollSeconds { get; set; } = 1;

        /// <summary>
        /// The local network port where the neonKUBE desktop application exposes
        /// the desktop service providing integration for the <b>neon-cli</b>
        /// command line tool.  This defaults to <see cref="KubeConst.DesktopServicePort"/>.
        /// </summary>
        [JsonProperty(PropertyName = "DesktopServicePort", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "desktopServicePort", ApplyNamingConventions = false)]
        [DefaultValue(KubeConst.DesktopServicePort)]
        public int DesktopServicePort { get; set; } = KubeConst.DesktopServicePort;

        /// <summary>
        /// The local network port where <b>kubectl proxy</b> will listen
        /// and forward traffic to the Kubernetes API server.  This 
        /// defaults to <see cref="KubeConst.KubectlProxyPort"/>.
        /// </summary>
        [JsonProperty(PropertyName = "KubectlProxyPort", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "kubectlProxyPort", ApplyNamingConventions = false)]
        [DefaultValue(KubeConst.KubectlProxyPort)]
        public int KubectlProxyPort { get; set; } = KubeConst.KubectlProxyPort;

        /// <summary>
        /// The local network port used for proxying requests to
        /// the Kubernetes dashboard for the current cluster.  This 
        /// defaults to <see cref="KubeConst.KubeDashboardProxyPort"/>.
        /// </summary>
        [JsonProperty(PropertyName = "KubeDashboardProxyPort", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "kubeDashboardProxyPort", ApplyNamingConventions = false)]
        [DefaultValue(KubeConst.KubeDashboardProxyPort)]
        public int KubeDashboardProxyPort { get; set; } = KubeConst.KubeDashboardProxyPort;

        /// <summary>
        /// The local network port used for proxying requests to
        /// the Kibanarnetes dashboard for the current cluster.  This 
        /// defaults to <see cref="KubeConst.KibanaDashboardProxyPort"/>.
        /// </summary>
        [JsonProperty(PropertyName = "KibanaDashboardProxyPort", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "KibanaDashboardProxyPort", ApplyNamingConventions = false)]
        [DefaultValue(KubeConst.KibanaDashboardProxyPort)]
        public int KibanaDashboardProxyPort { get; set; } = KubeConst.KibanaDashboardProxyPort;

        /// <summary>
        /// The local network port used for proxying requests to
        /// the Prometheusrnetes dashboard for the current cluster.  This 
        /// defaults to <see cref="KubeConst.PrometheusDashboardProxyPort"/>.
        /// </summary>
        [JsonProperty(PropertyName = "PrometheusDashboardProxyPort", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "PrometheusDashboardProxyPort", ApplyNamingConventions = false)]
        [DefaultValue(KubeConst.PrometheusDashboardProxyPort)]
        public int PrometheusDashboardProxyPort { get; set; } = KubeConst.PrometheusDashboardProxyPort;

        /// <summary>
        /// The local network port used for proxying requests to
        /// the Kialirnetes dashboard for the current cluster.  This 
        /// defaults to <see cref="KubeConst.KialiDashboardProxyPort"/>.
        /// </summary>
        [JsonProperty(PropertyName = "KialiDashboardProxyPort", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "KialiDashboardProxyPort", ApplyNamingConventions = false)]
        [DefaultValue(KubeConst.KialiDashboardProxyPort)]
        public int KialiDashboardProxyPort { get; set; } = KubeConst.KialiDashboardProxyPort;

        /// <summary>
        /// The local network port used for proxying requests to
        /// the Grafanarnetes dashboard for the current cluster.  This 
        /// defaults to <see cref="KubeConst.GrafanaDashboardProxyPort"/>.
        /// </summary>
        [JsonProperty(PropertyName = "GrafanaDashboardProxyPort", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "GrafanaDashboardProxyPort", ApplyNamingConventions = false)]
        [DefaultValue(KubeConst.GrafanaDashboardProxyPort)]
        public int GrafanaDashboardProxyPort { get; set; } = KubeConst.GrafanaDashboardProxyPort;

        /// <summary>
        /// Ensures that the state is valid.
        /// </summary>
        public void Validate()
        {
            // Generate a new installation ID if we don't have one or it's invalid.

            if (string.IsNullOrEmpty(InstallationId) || !Guid.TryParse(InstallationId, out var guid))
            {
                InstallationId = Guid.NewGuid().ToString("d");
            }

            if (StatusPollSeconds <= 0)
            {
                StatusPollSeconds = 10;
            }

            // Ensure that the proxy ports are valid.

            if (!NetHelper.IsValidPort(DesktopServicePort))
            {
                DesktopServicePort = KubeConst.DesktopServicePort;
            }

            if (!NetHelper.IsValidPort(KubectlProxyPort))
            {
                KubectlProxyPort = KubeConst.KubectlProxyPort;
            }

            if (!NetHelper.IsValidPort(KubeDashboardProxyPort))
            {
                KubeDashboardProxyPort = KubeConst.KubeDashboardProxyPort;
            }
        }
    }
}
