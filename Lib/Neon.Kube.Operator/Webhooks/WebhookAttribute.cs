//-----------------------------------------------------------------------------
// FILE:	    WebhookAttribute.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:	Copyright © 2005-2023 by NEONFORGE LLC.  All rights reserved.
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

using k8s.Models;

namespace Neon.Kube.Operator.Webhook
{
    /// <summary>
    /// Describes an admission webhook and the resources and operations it applies to.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class WebhookAttribute : Attribute
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="serviceName"></param>
        /// <param name="namespace"></param>
        /// <param name="certificate"></param>
        /// <param name="admissionReviewVersions"></param>
        /// <param name="failurePolicy"></param>
        /// <param name="sideEffects"></param>
        /// <param name="timeoutSeconds"></param>
        /// <param name="matchPolicy"></param>
        /// <param name="reinvocationPolicy"></param>
        /// <param name="url"></param>
        public WebhookAttribute(
            string name,
            string admissionReviewVersions,
            string serviceName = null,
            string @namespace = null,
            string certificate = null,
            string failurePolicy = "Fail", 
            string sideEffects = "None", 
            int timeoutSeconds = 5,
            string matchPolicy = "Equivalent",
            string reinvocationPolicy = "Never",
            string url = null)
        {
            Name = name;
            ServiceName = serviceName;
            Namespace = @namespace;
            Certificate = certificate;
            AdmissionReviewVersions = admissionReviewVersions.Split(',');
            FailurePolicy = failurePolicy;
            SideEffects = sideEffects;
            TimeoutSeconds = timeoutSeconds;
            MatchPolicy = matchPolicy;
            ReinvocationPolicy = reinvocationPolicy;
            Url = url;
        }

        /// <summary>
        /// The name of the admission webhook. Name should be fully qualified, e.g., imagepolicy.kubernetes.io, 
        /// where "imagepolicy" is the name of the webhook, and kubernetes.io is the name of the organization. 
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// The kubernetes service name for the webhook.
        /// </summary>
        public string ServiceName { get; }

        /// <summary>
        /// The namespace where the webhook is deployed.
        /// </summary>
        public string Namespace { get; }

        /// <summary>
        /// The certificate name. Formatted as namespace/name.
        /// </summary>
        public string Certificate { get; }

        /// <summary>
        /// <para>
        /// An ordered list of preferred `AdmissionReview` versions the Webhook expects.
        /// API server will try to use first version in the list which it supports. If none 
        /// of the versions specified in this list supported by API server, validation will 
        /// fail for this object. If a persisted webhook configuration specifies allowed 
        /// versions and does not include any versions known to the API Server, calls to the 
        /// webhook will fail and be subject to the failure policy.
        /// </para>
        /// </summary>
        public string[] AdmissionReviewVersions { get; }

        /// <summary>
        /// <para>
        /// Defines how unrecognized errors from the admission endpoint are handled - 
        /// allowed values are Ignore or Fail. Defaults to Fail.
        /// </para>
        /// </summary>
        public string FailurePolicy { get; } = "Fail";

        /// <summary>
        /// <para>
        /// States whether this webhook has side effects. Acceptable values are: None, 
        /// NoneOnDryRun (webhooks created via v1beta1 may also specify Some or Unknown).
        /// Webhooks with side effects MUST implement a reconciliation system, since a 
        /// request may be rejected by a future step in the admission chain and the side 
        /// effects therefore need to be undone. Requests with the dryRun attribute will 
        /// be auto-rejected if they match a webhook with sideEffects == Unknown or Some.
        /// </para>
        /// </summary>
        public string SideEffects { get; } = "None";

        /// <summary>
        /// <para>
        /// Specifies the timeout for this webhook. After the timeout passes, the webhook 
        /// call will be ignored or the API call will fail based on the failure policy. 
        /// The timeout value must be between 1 and 30 seconds.
        /// </para>
        /// </summary>
        public int TimeoutSeconds { get; } = 10;

        /// <summary>
        /// <para>
        /// Defines how the "rules" list is used to match incoming requests. Allowed values 
        /// are "Exact" or "Equivalent". - Exact: match a request only if it exactly matches 
        /// a specified rule. For example, if deployments can be modified via apps/v1, apps/v1beta1,
        /// and extensions/v1beta1, but "rules" only included `apiGroups:["apps"], 
        /// apiVersions:["v1"], resources: ["deployments"]`, a request to apps/v1beta1 or 
        /// extensions/v1beta1 would not be sent to the webhook. - Equivalent: match a request 
        /// if modifies a resource listed in rules, even via another API group or version. 
        /// For example, if deployments can be modified via apps/v1, apps/v1beta1, and 
        /// extensions/v1beta1, and "rules" only included `apiGroups:["apps"], apiVersions:["v1"],
        /// resources: ["deployments"]`, a request to apps/v1beta1 or extensions/v1beta1 would be 
        /// converted to apps/v1 and sent to the webhook. Defaults to "Equivalent"
        /// </para>
        /// </summary>
        public string MatchPolicy { get; } = "Equivalent";

        /// <summary>
        /// <para>
        /// Indicates whether this webhook should be called multiple times as part of a single 
        /// admission evaluation. Allowed values are "Never" and "IfNeeded". Never: the webhook 
        /// will not be called more than once in a single admission evaluation. IfNeeded: the webhook 
        /// will be called at least one additional time as part of the admission evaluation if the 
        /// object being admitted is modified by other admission plugins after the initial webhook 
        /// call. Webhooks that specify this option *must* be idempotent, able to process objects they
        /// previously admitted. Note: * the number of additional invocations is not guaranteed to be
        /// exactly one. * if additional invocations result in further modifications to the object, 
        /// webhooks are not guaranteed to be invoked again. * webhooks that use this option may be 
        /// reordered to minimize the number of additional invocations. * to validate an object after 
        /// all mutations are guaranteed complete, use a validating admission webhook instead.
        /// </para>
        /// Defaults to "Never".
        /// </summary>
        public string ReinvocationPolicy { get; } = "Never";

        /// <summary>
        /// The external URL of the webhook.
        /// </summary>
        public string Url { get; set; } = string.Empty;
    }
}