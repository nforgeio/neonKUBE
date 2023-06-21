//-----------------------------------------------------------------------------
// FILE:        RbacVerb.cs
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

namespace Neon.Kube.Operator.Rbac
{
    /// <summary>
    /// Enumerates the Kubernetes RBAC verbs.
    /// </summary>
    [Flags]
    public enum RbacVerb
    {
        /// <summary>
        /// No permissions will be allowed.
        /// </summary>
        None = 0,

        /// <summary>
        /// All permissions will be allowed.
        /// </summary>
        All = 1 << 0,

        /// <summary>
        /// Allows GET on the resource.
        /// </summary>
        Get = 1 << 1,

        /// <summary>
        /// Allows listing all resources for the type.
        /// </summary>
        List = 1 << 2,

        /// <summary>
        /// Allows watching resources of the type.
        /// </summary>
        Watch = 1 << 3,

        /// <summary>
        /// Allows creating resources for the type.
        /// </summary>
        Create = 1 << 4,

        /// <summary>
        /// Allows updating existing resources for the type.
        /// </summary>
        Update = 1 << 5,

        /// <summary>
        /// Allows patching resources for the type.
        /// </summary>
        Patch = 1 << 6,

        /// <summary>
        /// Allows deleting resources for the type.
        /// </summary>
        Delete = 1 << 7,
    }
}
