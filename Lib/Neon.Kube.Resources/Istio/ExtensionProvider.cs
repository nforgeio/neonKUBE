//-----------------------------------------------------------------------------
// FILE:        ExtensionProvider.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:   Copyright © 2005-2024 by NEONFORGE LLC.  All rights reserved.
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
using System.Runtime.Serialization;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

using Neon.Common;
using Neon.Net;

namespace Neon.Kube.Resources.Istio
{
    /// <summary>
    /// Identifies an Extension Provider.
    /// </summary>
    public class ExtensionProvider
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public ExtensionProvider()
        {
        }

        /// <summary>
        /// <para>
        /// Specifies the name of the extension provider. The list of available providers is defined in the MeshConfig. 
        /// Note, currently at most 1 extension provider is allowed per workload. Different workloads can use different 
        /// extension provider.
        /// </para>
        /// </summary>
        public string Name { get; set; } = null;
    }
}
