//-----------------------------------------------------------------------------
// FILE:	    ControllerAttribute.cs
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

using System;

using Neon.Kube.Resources;

namespace Neon.Kube.Operator.Attributes
{
    /// <summary>
    /// Used to exclude a component from assembly scanning when building the operator.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface)]
    public class ControllerAttribute : Attribute
    {
        /// <summary>
        /// Whether to ignore the controller when scanning assemblies.
        /// </summary>
        public bool Ignore { get; set; } = false;

        /// <summary>
        /// Optionally disable automatic finalizer registration. If enabled, all finalizers currently deployed by the operator
        /// will be registered when a resource is reconciled.
        /// </summary>
        public bool AutoRegisterFinalizers { get; set; } = true;

        /// <summary>
        /// Specifies whether Kubernetes custom resources should be created.
        /// </summary>
        public bool ManageCustomResourceDefinitions { get; set; } = true;

        /// <summary>
        /// An optional label selector to be applied to the watcher. This can be used for filtering.
        /// </summary>
        public string LabelSelector { get; set; } = null;

        /// <summary>
        /// An optional field selector to be applied to the watcher. This can be used for filtering.
        /// </summary>
        public string FieldSelector { get; set; } = null;

        /// <summary>
        /// Specifies the <see cref="ResourceManager.ResourceManagerOptions.MaxConcurrentReconciles"/>.
        /// </summary>
        public int MaxConcurrentReconciles { get; set; } = 1;

        /// <summary>
        /// Specifies the <see cref="ResourceManager.ResourceManagerOptions.ErrorMinRequeueInterval"/>.
        /// </summary>
        public int ErrorMinRequeueIntervalSeconds { get; set; } = 10;

        /// <summary>
        /// Specifies the <see cref="ResourceManager.ResourceManagerOptions.ErrorMaxRequeueInterval"/>.
        /// </summary>
        public int ErrorMaxRequeueIntervalSeconds { get; set; } = 600;
    }
}
