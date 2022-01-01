//-----------------------------------------------------------------------------
// FILE:	    BadBinaryInfo.cs
// CONTRIBUTOR: John Burns
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

using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace Neon.Temporal
{
    /// <summary>
    /// Defines information about a bad binary.
    /// </summary>
    public class BadBinaryInfo
    {
        /// <summary>
        /// Reason for bad binary.
        /// </summary>
        public string Reason { get; set; }

        /// <summary>
        /// The Operator of bad binary.
        /// </summary>
        public string Operator { get; set; }

        /// <summary>
        /// Creation time of bad binary.
        /// </summary>
        [JsonProperty(PropertyName = "create_time")]
        public DateTime? CreateTime { get; set; }
    }
}
