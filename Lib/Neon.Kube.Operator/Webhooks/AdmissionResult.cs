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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Http;

namespace Neon.Kube.Operator.Webhook
{
    /// <summary>
    /// Represents a result from an admission webhook.
    /// </summary>
    public class AdmissionResult
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        internal AdmissionResult()
        {
        }

        /// <summary>
        /// Whether the request was valid or not.
        /// </summary>
        public bool Valid { get; init; } = true;

        /// <summary>
        /// The http status code.
        /// </summary>
        public int? StatusCode { get; init; }

        /// <summary>
        /// The status message.
        /// </summary>
        public string StatusMessage { get; init; }

        /// <summary>
        /// Warnings associated with the result.
        /// </summary>
        public IList<string> Warnings { get; init; } = new List<string>();

        internal static TResult NotImplemented<TResult>()
            where TResult : AdmissionResult, new() => new()
            {
                Valid = false,
                StatusCode = StatusCodes.Status501NotImplemented,
                StatusMessage = "The method is not implemented.",
            };

    }
}