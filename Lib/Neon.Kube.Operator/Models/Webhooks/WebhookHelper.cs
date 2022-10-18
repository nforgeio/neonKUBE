//-----------------------------------------------------------------------------
// FILE:	    WebhookHelper.cs
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

using k8s;
using k8s.Models;
using Microsoft.AspNetCore.Http;

namespace Neon.Kube.Operator
{
    public sealed class WebhookHelper
    {
        public static string CreateEndpoint<TEntity>(Type webhook, WebhookType webhookType)
            where TEntity : IKubernetesObject<V1ObjectMeta>, new()
        {
            var metadata = typeof(TEntity).GetKubernetesTypeMetadata();

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
    }
}