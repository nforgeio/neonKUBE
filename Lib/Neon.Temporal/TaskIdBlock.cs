//-----------------------------------------------------------------------------
// FILE:	    TaskIdBlock.cs
// CONTRIBUTOR: John C. Burns
// COPYRIGHT:	Copyright (c) 2005-2021 by neonFORGE LLC.  All rights reserved.
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
using System.Text;

using Newtonsoft.Json;

namespace Neon.Temporal
{
    /// <summary>
    /// Defines a temporal task Id block
    /// </summary>
    public class TaskIdBlock
    {
        /// <summary>
        /// The long start id of the block.
        /// </summary>
        [JsonProperty(PropertyName = "start_id")]
        public long StartId { get; set; }

        /// <summary>
        /// The long end id of the block.
        /// </summary>
        [JsonProperty(PropertyName = "end_id")]
        public long EndId { get; set; }
    }
}
