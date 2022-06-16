//-----------------------------------------------------------------------------
// FILE:	    IIgnorableResource.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.
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
using System.Net.Http;

using Neon.Kube;

using k8s;

#if KUBEOPS
namespace Neon.Kube.ResourceDefinitions
#else
namespace Neon.Kube.Resources
#endif
{
    /// <summary>
    /// <para>
    /// Implemented by neonKUBE related custom resources to control whether the <b>ResourceManager</b>
    /// will ignore the presence of specific objects to work around this bug: https://github.com/nforgeio/neonKUBE/issues/1599.
    /// </para>
    /// </summary>
    public interface IIgnorableResource
    {
        /// <summary>
        /// Determines whether the resource instance should be recognized by <b>ResourceManager</b>
        /// and reported to the operator's controller.
        /// </summary>
        /// <returns><c>true</c> when the resource should be ignored.</returns>
        bool IsIgnorable();
    }
}
