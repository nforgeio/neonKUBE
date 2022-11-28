//-----------------------------------------------------------------------------
// FILE:	    NamespaceSelector.cs
// CONTRIBUTOR: Marcus Bowyer
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
using System.ComponentModel;
using System.Linq;
using System.Text;

using k8s;
using k8s.Models;

using Newtonsoft.Json;

namespace Neon.Kube
{
    /// <summary>
    /// NamespaceSelector is a selector for selecting either all namespaces or a list of namespaces. If any is true,
    /// it takes precedence over matchNames. If matchNames is empty and any is false, it means that the objects are 
    /// selected from the current namespace.
    /// </summary>
    public class NamespaceSelector
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        public NamespaceSelector()
        {
        }

        /// <summary>
        /// Boolean describing whether all namespaces are selected in contrast to a list restricting them.
        /// </summary>
        [DefaultValue(null)]
        public bool Any { get; set; }

        /// <summary>
        /// List of namespace names to select from.
        /// </summary>
        [DefaultValue(null)]
        public List<string> MatchNames { get; set; }
    }
}
