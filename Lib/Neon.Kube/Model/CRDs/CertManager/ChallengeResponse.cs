//-----------------------------------------------------------------------------
// FILE:	    ChallengeResponse.cs
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
using System.Threading.Tasks;

using Newtonsoft.Json;

namespace Neon.Kube
{
    /// <summary>
    /// 
    /// </summary>
    public class ChallengeResponse
    {
        /// <summary>
        /// <para>
        /// UID is an identifier for the individual request/response. It allows us to distinguish instances of requests which are
        /// otherwise identical (parallel requests, requests when earlier requests did not modify etc)
        /// </para>
        /// <para>
        /// The UID is meant to track the round trip (request/response) between the KAS and the WebHook, not the user request.
        /// It is suitable for correlating log entries between the webhook and apiserver, for either auditing or debugging.
        /// </para>
        /// </summary>
        [JsonProperty(PropertyName = "uid", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string Uid { get; set; }

        /// <summary>
        /// Indicates whether the request was successful.
        /// </summary>
        [JsonProperty(PropertyName = "success", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public bool Success { get; set; }
    }
}
