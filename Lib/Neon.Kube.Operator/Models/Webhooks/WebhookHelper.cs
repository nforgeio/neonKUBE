//-----------------------------------------------------------------------------
// FILE:	    WebhookHelper.cs
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
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using k8s;
using k8s.Models;
using Microsoft.AspNetCore.Http;

namespace Neon.Kube.Operator
{
    /// <summary>
    /// Webhook helper methods.
    /// </summary>
    public static class WebhookHelper
    {
        /// <summary>
        /// Helper method to create a route for an <see cref="IAdmissionWebhook{TEntity, TResult}"/>
        /// </summary>
        /// <typeparam name="TEntity"></typeparam>
        /// <param name="webhook"></param>
        /// <param name="webhookType"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public static string CreateEndpoint<TEntity>(Type webhook, WebhookType webhookType)
            where TEntity : IKubernetesObject<V1ObjectMeta>, new()
        {
            return CreateEndpoint(typeof(TEntity), webhook, webhookType);
        }

        /// <summary>
        /// Helper method to create a route for an <see cref="IAdmissionWebhook{TEntity, TResult}"/>
        /// </summary>
        /// <param name="webhook"></param>
        /// <param name="webhookType"></param>
        /// <param name="entityType"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public static string CreateEndpoint(Type entityType, Type webhook, WebhookType webhookType)
        {
            var metadata = entityType.GetKubernetesTypeMetadata();

            var builder = new StringBuilder();

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

            builder.Append($"/{webhook.Name}");

            switch (webhookType)
            {
                case WebhookType.Mutate:
                    builder.Append("/mutate");
                    break;

                case WebhookType.Validate:
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
        /// <param name="operations"></param>
        /// <returns></returns>
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