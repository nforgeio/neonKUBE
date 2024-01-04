//-----------------------------------------------------------------------------
// FILE:        V1ServiceMonitorSpec.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:   Copyright © 2005-2024 by NEONFORGE LLC.  All rights reserved.
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
using System.Linq;
using System.Text;

using k8s;
using k8s.Models;

using Newtonsoft.Json;

namespace Neon.Kube.Resources.Prometheus
{
    /// <summary>
    /// ServiceMonitorSpec contains specification parameters for a ServiceMonitor.
    /// </summary>
    public class V1ServiceMonitorSpec
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        public V1ServiceMonitorSpec()
        {
        }

        /// <summary>
        /// Chooses the label of the Kubernetes Endpoints. Its value will be used for the job-label's value of
        /// the created metrics.
        /// Default fallback value: the name of the respective Kubernetes Endpoint.
        /// </summary>
        public string JobLabel { get; set; }

        /// <summary>
        /// TargetLabels transfers labels from the Kubernetes Service onto the created metrics.
        /// </summary>
        public List<string> TargetLabels { get; set; }

        /// <summary>
        /// PodTargetLabels transfers labels on the Kubernetes Pod onto the created metrics.
        /// </summary>
        public List<string> PodTargetLabels { get; set; }

        /// <summary>
        /// A list of <see cref="Endpoint"/> allowed as part of this ServiceMonitor.
        /// </summary>
        public List<Endpoint> Endpoints { get; set; }

        /// <summary>
        /// <see cref="NamespaceSelector"/> to select which namespaces the Kubernetes Endpoints objects are discovered from.
        /// </summary>
        public NamespaceSelector NamespaceSelector { get; set; }

        /// <summary>
        /// <see cref="V1LabelSelector"/> to select Endpoints objects.
        /// </summary>
        public V1LabelSelector Selector { get; set; }

        /// <summary>
        /// SampleLimit defines per-scrape limit on number of scraped samples that will be accepted.
        /// </summary>
        public int? SampleLimit { get; set; }

        /// <summary>
        /// TargetLimit defines a limit on the number of scraped targets that will be accepted. 
        /// </summary>
        public int? TargetLimit { get; set; }

        /// <summary>
        /// Per-scrape limit on number of labels that will be accepted for a sample. 
        /// <note>Only valid in Prometheus versions 2.27.0 and newer.</note>
        /// </summary>
        public int? LabelLimit { get; set; }

        /// <summary>
        /// Per-scrape limit on length of labels name that will be accepted for a sample.
        /// <note>Only valid in Prometheus versions 2.27.0 and newer.</note>
        /// </summary>
        public int? LabelNameLengthLimit { get; set; }

        /// <summary>
        /// Per-scrape limit on length of labels value that will be accepted for a sample. 
        /// <note>Only valid in Prometheus versions 2.27.0 and newer.</note>
        /// </summary>
        public int? LabelValueLengthLimit { get; set; }
    }
}
