//-----------------------------------------------------------------------------
// FILE:	    AdmissionResult.cs
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

using k8s;

namespace Neon.Kube.Operator.Webhook
{
    internal sealed class AdmissionReview<TEntity> : IKubernetesObject
    {
        public AdmissionReview()
        {
        }
        public AdmissionReview(AdmissionResponse response) => Response = response;

        public string ApiVersion { get; set; } = "admission.k8s.io/v1";

        public string Kind { get; set; } = "AdmissionReview";

        public AdmissionRequest<TEntity> Request { get; set; }

        public AdmissionResponse Response { get; set; }
    }
}