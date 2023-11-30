//-----------------------------------------------------------------------------
// FILE:        V1AuthorizationPolicySpec.cs
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

using System.Collections.Generic;
using System.ComponentModel;

using Newtonsoft.Json;

namespace Neon.Kube.Resources.Istio
{
    /// <summary>
    /// Describes the <see cref="V1AuthorizationPolicy"/> spec.
    /// </summary>
    public class V1AuthorizationPolicySpec
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        public V1AuthorizationPolicySpec()
        {
        }

        /// <summary>
        /// <para>
        /// The selector decides where to apply the authorization policy. The selector will match with workloads in the same namespace as the 
        /// authorization policy. If the authorization policy is in the root namespace, the selector will additionally match with workloads 
        /// in all namespaces.
        /// </para>
        /// </summary>
        [DefaultValue(null)]
        public WorkloadSelector Selector { get; set; } = null;

        /// <summary>
        /// <para>
        /// A list of rules to match the request. A match occurs when at least one rule matches the request.
        /// If not set, the match will never occur. This is equivalent to setting a default of deny for the target workloads 
        /// if the action is ALLOW.
        /// </para>
        /// </summary>
        [DefaultValue(null)]
        public List<AuthorizationPolicyRule> Rules { get; set; } = null;

        /// <summary>
        /// <para>
        /// The action to take if the request is matched with the rules. Default is ALLOW if not specified.
        /// </para>
        /// </summary>
        [DefaultValue(AuthorizationPolicyAction.Allow)]
        public AuthorizationPolicyAction Action { get; set; } = AuthorizationPolicyAction.Allow;

        /// <summary>
        /// <para>
        /// Specifies detailed configuration of the CUSTOM action. Must be used only with CUSTOM action. 
        /// </para>
        /// </summary>
        [DefaultValue(null)]
        public ExtensionProvider Provider { get; set; }
    }
}
