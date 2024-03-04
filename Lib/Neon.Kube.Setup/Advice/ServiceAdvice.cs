//-----------------------------------------------------------------------------
// FILE:        KubeServiceAdvice.cs
// CONTRIBUTOR: Jeff Lill
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
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Neon.Collections;
using Neon.Common;
using Neon.Cryptography;
using Neon.IO;
using Neon.Retry;
using Neon.SSH;
using Neon.Tasks;

namespace Neon.Kube.Setup
{
    /// <summary>
    /// Used by <see cref="ClusterAdvice"/> to record configuration advice for a specific
    /// Kurbernetes service being deployed.
    /// </summary>
    public class ServiceAdvice
    {
        private ClusterAdvice   clusterAdvice;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="clusterAdvice">Specifies the parent <see cref="ClusterAdvice"/>.</param>
        /// <param name="serviceName">Identifies the service.</param>
        public ServiceAdvice(ClusterAdvice clusterAdvice, string serviceName)
        {
            Covenant.Requires<ArgumentNullException>(clusterAdvice != null, nameof(clusterAdvice));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(serviceName), nameof(serviceName));

            this.clusterAdvice = clusterAdvice;
            this.ServiceName   = serviceName;
        }

        /// <summary>
        /// Returns the service name.
        /// </summary>
        public string ServiceName { get; private set; }

        /// <summary>
        /// <para>
        /// Cluster advice is designed to be configured once during cluster setup and then be
        /// considered to be <b>read-only</b> thereafter.  This property should be set to 
        /// <c>true</c> after the advice is intialized to prevent it from being modified
        /// again.
        /// </para>
        /// <note>
        /// This is necessary because setup is performed on multiple threads and this class
        /// is not inheritly thread-safe.  This also fits with the idea that the logic behind
        /// this advice is to be centralized.
        /// </note>
        /// </summary>
        public bool IsReadOnly { get; internal set; }

        /// <summary>
        /// Ensures that <see cref="IsReadOnly"/> isn't <c>true.</c>
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown then <see cref="IsReadOnly"/> is <c>true</c>.</exception>
        private void EnsureNotReadOnly()
        {
            if (IsReadOnly)
            {
                throw new InvalidOperationException("Cluster advice is read-only.");
            }
        }

        private double? podCpuLimit;

        /// <summary>
        /// Specifies the CPU limit for each service pod or <c>null</c> when this property is not set.
        /// </summary>
        public double? PodCpuLimit
        {
            get { return podCpuLimit; }
            set { EnsureNotReadOnly(); podCpuLimit = value; }
        }

        private double? podCpuRequest;

        /// <summary>
        /// Specifies the CPU request for each service pod or <c>null</c> when this property is not set.
        /// </summary>
        public double? PodCpuRequest
        {
            get { return podCpuRequest; }
            set { EnsureNotReadOnly(); podCpuRequest = value; }
        }

        private decimal? podMemoryLimit;

        /// <summary>
        /// Specifies the memory limit for each service pod or <c>null</c> when this property is not set.
        /// </summary>
        public decimal? PodMemoryLimit
        {
            get { return podMemoryLimit; }
            set { EnsureNotReadOnly(); podMemoryLimit = value; }
        }

        private decimal? podMemoryRequest;

        /// <summary>
        /// Specifies the memory request for each service pod or <c>null</c> when this property is not set.
        /// </summary>
        public decimal? PodMemoryRequest
        {
            get { return podMemoryRequest;  }
            set { EnsureNotReadOnly(); podMemoryRequest = value; }
        }

        private int  replicaCount = 1;

        /// <summary>
        /// Specifies the number of pods to be seployed for the service or <b>1</b> when this property is not set.
        /// </summary>
        public int Replicas
        {
            get { return replicaCount; }
            set { EnsureNotReadOnly(); replicaCount = value; }
        }

        private bool? metricsEnabled;

        /// <summary>
        /// <para>
        /// Specifies whether metrics should be collected for the service.
        /// </para>
        /// <note>
        /// <see cref="ClusterAdvice.MetricsEnabled"/> will be returned when this
        /// property isn't set explicitly.
        /// </note>
        /// </summary>
        public bool MetricsEnabled
        {
            get { return metricsEnabled ?? clusterAdvice.MetricsEnabled; }
            set { EnsureNotReadOnly(); metricsEnabled = value; }
        }

        private string metricsInterval;

        /// <summary>
        /// <para>
        /// Specifies the metrics scrape interval or <c>null</c> when this property is not set.
        /// </para>
        /// <note>
        /// <see cref="ClusterAdvice.MetricsEnabled"/> will be returned when this
        /// property isn't set explicitly.
        /// </note>
        /// </summary>
        public string MetricsInterval
        {
            get { return metricsInterval ?? clusterAdvice.MetricsInterval; }
            set { EnsureNotReadOnly(); metricsInterval = value; }
        }

        /// <summary>
        /// Used to obtain the metrics port exposed for a service when cluster
        /// metrics are enabled.  This is useful for setting Helm chart <b>metricsPort</b>
        /// values where an empty string disables metrics(e.g. for OpenEBS).
        /// </summary>
        /// <param name="port">
        /// Specifies the metrics port exposed for the service or zero when
        /// metrics are to be disabled for the service.
        /// </param>
        /// <returns>An empty string when cluster metrics are disabled or <paramref name="port"/> (as a string) otherwise.</returns>
        public string GetMetricsPort(int port)
        {
            return MetricsEnabled ? port.ToString() : string.Empty;
        }

        private string nodeSelector = "{}";

        /// <summary>
        /// Returns the single-line node selector object for the service or <b>"{}"</b>
        /// when this is unconstrained.
        /// </summary>
        public string NodeSelector
        {
            get { return nodeSelector; }
            set { EnsureNotReadOnly(); nodeSelector = value; }
        }

        private string priorityClassName = PriorityClass.NeonMin.Name;

        /// <summary>
        /// Returns the priority class name for the service.  This defaults to
        /// <see cref="PriorityClass.NeonMin"/>.
        /// </summary>
        public string PriorityClassName
        {
            get { return priorityClassName; }
            set { EnsureNotReadOnly(); priorityClassName = value; }
        }
    }
}
