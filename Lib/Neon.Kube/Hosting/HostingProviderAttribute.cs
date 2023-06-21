//-----------------------------------------------------------------------------
// FILE:        HostingProviderAttribute.cs
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
using System.Text;

using Neon.Kube.ClusterDef;

namespace Neon.Kube.Hosting
{
    /// <summary>
    /// Use this attribute to identify <see cref="IHostingManager"/> class implementations
    /// so they can be discovered by the <see cref="HostingManager"/> class.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class HostingProviderAttribute : Attribute
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="environment">Specifies the target hosting environment.</param>
        public HostingProviderAttribute(HostingEnvironment environment)
        {
            this.Environment = environment;
        }

        /// <summary>
        /// Returns the target hosting environment supported by the tagged <see cref="IHostingManager"/>.
        /// </summary>
        public HostingEnvironment Environment { get; private set; }
    }
}
