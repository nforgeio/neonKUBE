//-----------------------------------------------------------------------------
// FILE:	    ResourceManagerMode.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.
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

namespace Neon.Kube.Operator
{
    /// <summary>
    /// Specifies how a resource manager should treat reconciled resources.  See the 
    /// <see cref="ResourceManager{TResource, TController}"/> remarks for more information.
    /// </summary>
    public enum ResourceManagerMode
    {
        /// <summary>
        /// Configures a resource controller such that it works pretty much like a standard operator SDK
        /// where resources are resolved immediately when they are received.
        /// </summary>
        Normal = 0,

        /// <summary>
        /// Configures a resource controller such that it maintains a collection of reconciled resources
        /// and then includes this set along with the changed resource.  This is useful for scenarios
        /// where the entire collection of resources is required to perform operations.
        /// </summary>
        Collection
    }
}
