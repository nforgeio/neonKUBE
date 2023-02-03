﻿//-----------------------------------------------------------------------------
// FILE:	    OperatorSettings.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:	Copyright © 2005-2023 by NEONFORGE LLC.  All rights reserved.
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

using k8s;
using Neon.Kube.Operator.ResourceManager;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Neon.Kube.Operator
{
    /// <summary>
    /// Specifies global options for the Operator.
    /// </summary>
    public class OperatorSettings
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public OperatorSettings()
        {
        }

        /// <summary>
        /// The Kubernetes client configuration.
        /// </summary>
        public KubernetesClientConfiguration KubernetesClientConfiguration { get; set; }

        /// <summary>
        /// <para>
        /// Specifies whether assembly scanning should be enabled. If enabled, Controllers, Finalizers and Webhooks will
        /// be scanned and added automatically. Defaults to true.
        /// </para>
        /// </summary>
        public bool AssemblyScanningEnabled { get; set; } = true;

        /// <summary>
        /// The size of the pool to use for async locks.
        /// </summary>
        public int LockPoolSize { get; set; } = 20;

        /// <summary>
        /// The number of items to fill the lock pool with during initialization.
        /// </summary>
        public int LockPoolInitialFill { get; set; } = 1;

        /// <summary>
        /// Default resource manager options that will be applied to controllers unless overridden.
        /// </summary>
        public ResourceManagerOptions ResourceManagerOptions { get; set; } = new ResourceManagerOptions();

        /// <summary>
        /// Validates the option properties.
        /// </summary>
        /// <exception cref="ValidationException">Thrown when any of the properties are invalid.</exception>
        public void Validate()
        {
            if (LockPoolInitialFill > LockPoolSize)
            {
                throw new ValidationException($"{nameof(LockPoolInitialFill)} can't be larger than {nameof(LockPoolSize)}");
            }
        }
    }
}