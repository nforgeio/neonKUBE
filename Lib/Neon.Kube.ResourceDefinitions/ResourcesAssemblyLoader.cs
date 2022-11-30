//-----------------------------------------------------------------------------
// FILE:	    Stub.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright © 2005-2022 by NEONFORGE LLC.  All rights reserved.
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
using System.Text;

namespace Neon.Kube.ResourceDefinitions
{
    /// <summary>
    /// Called by operators to ensure that the <b>Neon.Kube.ResourceDefinitions</b>
    /// assembly loaded into the current <see cref="AppDomain"/> so KubeOps will be
    /// able to reflect the resource definitions and generate the CRDs etc.
    /// </summary>
    public static class ResourcesAssemblyLoader
    {
        /// <summary>
        /// Ensures that the <b>Neon.Kube.ResourceDefinitions</b> assembly is loaded.
        /// </summary>
        public static void Load()
        {
            // This is a NOP because just calling this ensures that the 
            // assembly is loaded.
        }
    }
}
