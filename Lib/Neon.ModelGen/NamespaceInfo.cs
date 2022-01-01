//-----------------------------------------------------------------------------
// FILE:	    NamespaceInfo.cs
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
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Text;

namespace Neon.ModelGen
{
    /// <summary>
    /// Used to collect information about the items to be generated 
    /// within an output namespace.
    /// </summary>
    internal class NamespaceInfo
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="outputNamespace">The output namespace name.</param>
        public NamespaceInfo(string outputNamespace)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(outputNamespace), nameof(outputNamespace));

            this.OutputNamespace = outputNamespace;
        }

        /// <summary>
        /// Returns the output namspace name.
        /// </summary>
        public string OutputNamespace { get; private set; }

        /// <summary>
        /// Lists the data models to be generated within this namespace.
        /// </summary>
        public List<DataModel> DataModels { get; private set; } = new List<DataModel>();

        /// <summary>
        /// Lists the service models to be generated within this namespace.
        /// </summary>
        public List<ServiceModel> ServiceModels { get; private set; } = new List<ServiceModel>();
    }
}
