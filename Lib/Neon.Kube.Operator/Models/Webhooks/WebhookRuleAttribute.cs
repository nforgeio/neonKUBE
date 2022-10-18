//-----------------------------------------------------------------------------
// FILE:	    WebhookAttribute.cs
// CONTRIBUTOR: Marcus Bowyer
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
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Neon.Kube.Operator
{
    /// <summary>
    /// Rules describes what operations on what resources/subresources the 
    /// webhook cares about. The webhook cares about an operation if it matches
    /// _any_ Rule. However, in order to prevent ValidatingAdmissionWebhooks 
    /// and MutatingAdmissionWebhooks from putting the cluster in a state which 
    /// cannot be recovered from without completely disabling the plugin, 
    /// ValidatingAdmissionWebhooks and MutatingAdmissionWebhooks are never called 
    /// on admission requests for ValidatingWebhookConfiguration and 
    /// MutatingWebhookConfiguration objects.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class WebhookRuleAttribute : Attribute
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="apiGroups"></param>
        /// <param name="apiVersions"></param>
        /// <param name="operations"></param>
        /// <param name="resources"></param>
        /// <param name="scope"></param>
        public WebhookRuleAttribute(
            string apiGroups, 
            string apiVersions,
            AdmissionOperations operations, 
            string resources, 
            string scope)
        {
            ApiGroups = apiGroups.Split(',');
            ApiVersions = apiVersions.Split(',');
            Operations = operations;
            Resources = resources.Split(',');
            Scope = scope;
        }

        /// <summary>
        /// APIGroups is the API groups the resources belong to. '\*' is all groups. 
        /// If '\*' is present, the length of the slice must be one.
        /// </summary>
        public string[] ApiGroups { get; }

        /// <summary>
        /// APIVersions is the API versions the resources belong to. '\*' is all 
        /// versions. If '\*' is present, the length of the slice must be one.
        /// </summary>
        public string[] ApiVersions { get; }

        /// <summary>
        /// The operations the admission hook cares about - CREATE, 
        /// UPDATE, DELETE, CONNECT or * for all of those operations and any future 
        /// admission operations that are added. If '\*' is present, the length of 
        /// the slice must be one.
        /// </summary>
        public AdmissionOperations Operations { get; }

        /// <summary>
        /// A list of resources this rule applies to. For example: 'pods' 
        /// means pods. 'pods/log' means the log subresource of pods. '\*' means all 
        /// resources, but not subresources. 'pods/\*' means all subresources of pods. 
        /// '\*/scale' means all scale subresources. '\*/\*' means all resources and 
        /// their subresources. If wildcard is present, the validation rule will ensure 
        /// resources do not overlap with each other. Depending on the enclosing object, 
        /// subresources might not be allowed.
        /// </summary>
        public string[] Resources { get; }

        /// <summary>
        /// Specifies the scope of this rule. Valid values are "Cluster", "Namespaced", 
        /// and "*" "Cluster" means that only cluster-scoped resources will match this 
        /// rule. Namespace API objects are cluster-scoped. "Namespaced" means that only 
        /// namespaced resources will match this rule. "*" means that there are no scope 
        /// restrictions. Subresources match the scope of their parent resource.
        /// </summary>
        public string Scope { get; }
    }

}
