//-----------------------------------------------------------------------------
// FILE:        CustomResourceAttribute.cs
// CONTRIBUTOR: Marcus Bowyer
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

using Neon.Kube.Resources;

using k8s.Models;
using k8s;

namespace Neon.Kube.Resources.Attributes
{
    /// <summary>
    /// Used for versiond custom resources.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class EntityVersionAttribute : Attribute
    {
        /// <summary>
        /// Each version can be enabled/disabled by Served flag.
        /// </summary>
        public bool Served { get; set; } = true;

        /// <summary>
        /// One and only one version must be marked as the storage version.
        /// </summary>
        public bool Storage { get; set; } = false;
    }
}
