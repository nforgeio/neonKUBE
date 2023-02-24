//-----------------------------------------------------------------------------
// FILE:	    WebhookHelper.cs
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

using Microsoft.AspNetCore.Http;

using k8s;
using k8s.Models;
using System.Diagnostics.Contracts;

namespace Neon.Kube.Operator.Webhook
{
    /// <summary>
    /// Webhook helper methods.
    /// </summary>
    public static class WebhookHelper
    {
        /// <summary>
        /// Helper method to create a route for an <see cref="IAdmissionWebhook{TEntity, TResult}"/>
        /// </summary>
        /// <typeparam name="TEntity">Specifies the entity type.</typeparam>
        /// <param name="webhookImplementation">Specifies the web hook implementation type.</param>
        /// <param name="webhookType">Specifies the webook type.</param>
        /// <returns>The endpoint string.</returns>
        public static string CreateEndpoint<TEntity>(Type webhookImplementation, WebhookType webhookType)
            where TEntity : IKubernetesObject<V1ObjectMeta>, new()
        {
            Covenant.Requires<ArgumentNullException>(webhookImplementation != null, nameof(webhookImplementation));

            return CreateEndpoint(typeof(TEntity), webhookImplementation, webhookType);
        }

        /// <summary>
        /// Helper method to create a route for an <see cref="IAdmissionWebhook{TEntity, TResult}"/>
        /// </summary>
        /// <param name="entityType">Specifies the associated kubernetes entity type.</param>
        /// <param name="webhookImplementation">Specifies the web hook implementation type.</param>
        /// <param name="webhookType">Specifies the webook type.</param>
        /// <returns>The endpoint string.</returns>
        public static string CreateEndpoint(Type entityType, Type webhookImplementation, WebhookType webhookType)
        {
            Covenant.Requires<ArgumentNullException>(entityType != null, nameof(entityType));

            var metadata = entityType.GetKubernetesTypeMetadata();
            var builder  = new StringBuilder();

            if (!string.IsNullOrEmpty(metadata.Group))
            {
                builder.Append($"/{metadata.Group}");
            }

            if (!string.IsNullOrEmpty(metadata.ApiVersion))
            {
                builder.Append($"/{metadata.ApiVersion}");
            }

            if (!string.IsNullOrEmpty(metadata.PluralName))
            {
                builder.Append($"/{metadata.PluralName}");
            }

            builder.Append($"/{webhookImplementation.Name}");

            switch (webhookType)
            {
                case WebhookType.Mutating:

                    builder.Append("/mutate");
                    break;

                case WebhookType.Validating:

                    builder.Append("/validate");
                    break;

                default:

                    throw new ArgumentException();
            }

            return builder.ToString().ToLowerInvariant();
        }

        /// <summary>
        /// Returns a list of strings representing allowed operations.
        /// </summary>
        /// <param name="operations">The admission operations.</param>
        /// <returns>The admission operation strings.</returns>
        public static List<string> ToList(this AdmissionOperations operations)
        {
            if (operations.HasFlag(AdmissionOperations.All))
            {
                return new List<string> { "*" };
            }

            var result = new List<string>();

            if (operations.HasFlag(AdmissionOperations.Create))
            {
                result.Add("CREATE");
            }

            if (operations.HasFlag(AdmissionOperations.Update))
            {
                result.Add("UPDATE");
            }

            if (operations.HasFlag(AdmissionOperations.Delete))
            {
                result.Add("DELETE");
            }

            return result;
        }
    }
}