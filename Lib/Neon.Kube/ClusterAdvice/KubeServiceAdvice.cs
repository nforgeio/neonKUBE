//-----------------------------------------------------------------------------
// FILE:        KubeServiceAdvice.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright © 2005-2023 by NEONFORGE LLC.  All rights reserved.
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

namespace Neon.Kube
{
    /// <summary>
    /// Used by <see cref="KubeClusterAdvice"/> to record configuration advice for a specific
    /// Kurbernetes service being deployed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class along with an early setup step is intended to be used to help centralize the
    /// logic that decides things like what resources to reserve for services, how many service
    /// pods to deploy as well as whether to control which nodes nodes a service's pods may be
    /// deployed via affinity/tainting.
    /// </para>
    /// </remarks>
    public class KubeServiceAdvice : ObjectDictionary
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// <see cref="double"/>: Identifies the property specifying the maximum
        /// CPU to assign to each service pod.
        /// </summary>
        public const string PodCpuLimitProperty = "pod.cpu.limit";

        /// <summary>
        /// <see cref="double"/>: Identifies the property specifying the CPU to 
        /// reserve for each service pod.
        /// </summary>
        public const string PodCpuRequestProperty = "pod.cpu.request";
        
        /// <summary>
        /// <see cref="decimal"/>: Identifies the property specifying the maxumum
        /// bytes RAM that can be consumed by each service pod.
        /// </summary>
        public const string PodMemoryLimitProperty = "pod.memory.limit";

        /// <summary>
        /// <see cref="decimal"/>: Identifies the property specifying the bytes of
        /// RAM to be reserved for each service pod.
        /// </summary>
        public const string PodMemoryRequestProperty = "pod.memory.request";

        /// <summary>
        /// <see cref="int"/>: Identifies the property specifying how many pods
        /// should be deployed for the service.
        /// </summary>
        public const string ReplicaCountProperty = "replica.count";

        /// <summary>
        /// <see cref="int"/>: Identifies the property specifying whether metrics
        /// are enabled for the service.
        /// </summary>
        public const string MetricsEnabledProperty = "metrics.enabled";

        /// <summary>
        /// <see cref="int"/>: Identifies the property specifying how often metrics
        /// should be scraped for the service.
        /// </summary>
        public const string MetricsIntervalProperty = "metrics.interval";

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="serviceIdentity">Identifies the service.</param>
        public KubeServiceAdvice(string serviceIdentity)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(serviceIdentity));

            this.ServiceName = serviceIdentity;
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
        /// Returns the property value if present or <c>null</c>.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="name">The property name.</param>
        /// <returns>The property value or <c>null</c>.</returns>
        public Nullable<T> GetProperty<T>(string name)
            where T : struct
        {
            if (TryGetValue<T>(name, out var value))
            {
                return value as Nullable<T>;
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Returns the property value if present or <c>null</c>.
        /// </summary>
        /// <param name="name">The property name.</param>
        /// <returns>The property value or <c>null</c>.</returns>
        public string GetProperty(string name)
        {
            if (TryGetValue(name, out var value))
            {
                return value as string;
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Sets the property value or removes it when the value passed is <c>null</c>.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="name">The property name.</param>
        /// <param name="value">The property value or <c>null</c>.</param>
        /// <exception cref="InvalidOperationException">Thrown when the instance is in read-only mode.</exception>
        private void SetProperty<T>(string name, Nullable<T> value)
            where T : struct
        {
            if (IsReadOnly)
            {
                throw new InvalidOperationException($"[{nameof(KubeServiceAdvice)}] is read-only.");
            }

            if (value.HasValue)
            {
                base[name] = value;
            }
            else
            {
                if (base.ContainsKey(name))
                {
                    base.Remove(name);
                }
            }
        }

        /// <summary>
        /// Sets the property value or removes it when the value passed is <c>null</c>.
        /// </summary>
        /// <param name="name">The property name.</param>
        /// <param name="value">The property value or <c>null</c>.</param>
        /// <exception cref="InvalidOperationException">Thrown when the instance is in read-only mode.</exception>
        private void SetProperty(string name, string value)
        {
            if (IsReadOnly)
            {
                throw new InvalidOperationException($"[{nameof(KubeServiceAdvice)}] is read-only.");
            }

            if (!string.IsNullOrEmpty(value))
            {
                base[name] = value;
            }
            else
            {
                if (base.ContainsKey(name))
                {
                    base.Remove(name);
                }
            }
        }

        /// <summary>
        /// Specifies the CPU limit for each service pod or <c>null</c> when this property is not set.
        /// </summary>
        public double? PodCpuLimit
        {
            get => GetProperty<double>(PodCpuLimitProperty);
            set => SetProperty<double>(PodCpuLimitProperty, value);
        }

        /// <summary>
        /// Specifies the CPU request for each service pod or <c>null</c> when this property is not set.
        /// </summary>
        public double? PodCpuRequest
        {
            get => GetProperty<double>(PodCpuRequestProperty);
            set => SetProperty<double>(PodCpuRequestProperty, value);
        }


        /// <summary>
        /// Specifies the memory limit for each service pod or <c>null</c> when this property is not set.
        /// </summary>
        public decimal? PodMemoryLimit
        {
            get => GetProperty<decimal>(PodMemoryLimitProperty);
            set => SetProperty<decimal>(PodMemoryLimitProperty, value);
        }

        /// <summary>
        /// Specifies the memory request for each service pod or <c>null</c> when this property is not set.
        /// </summary>
        public decimal? PodMemoryRequest
        {
            get => GetProperty<decimal>(PodMemoryRequestProperty);
            set => SetProperty<decimal>(PodMemoryRequestProperty, value);
        }

        /// <summary>
        /// Specifies the number of pods to be seployed for the service or <c>null</c> when this property is not set.
        /// </summary>
        public int? ReplicaCount
        {
            get => GetProperty<int>(ReplicaCountProperty);
            set => SetProperty<int>(ReplicaCountProperty, value);
        }

        /// <summary>
        /// Specifies whether metrics should be collected for the service.
        /// </summary>
        public bool? MetricsEnabled
        {
            get => GetProperty<bool>(MetricsEnabledProperty);
            set => SetProperty<bool>(MetricsEnabledProperty, value);
        }

        /// <summary>
        /// Specifies the metrics scrape interval or <c>null</c> when this property is not set.
        /// </summary>
        public string MetricsInterval
        {
            get => GetProperty(MetricsIntervalProperty);
            set => SetProperty(MetricsIntervalProperty, value);
        }
    }
}
