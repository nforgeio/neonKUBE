//-----------------------------------------------------------------------------
// FILE:	    Completion.cs
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
using System.Buffers;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Neon.Common;

using Newtonsoft.Json;

namespace Neon.SignalR
{
    internal class Completion
    {
        [JsonProperty(PropertyName = "CompletionMessage", Required = Required.Always, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public ReadOnlySequence<byte> CompletionMessage { get; set; }

        [JsonProperty(PropertyName = "ProtocolName", Required = Required.Always, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string ProtocolName { get; set; }

        public Completion(string protocolName, ReadOnlySequence<byte> completionMessage)
        {
            ProtocolName      = protocolName;
            CompletionMessage = completionMessage;
        }

        /// <summary>
        /// Reads an <see cref="Completion"/> from a byte array.
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        public static Completion Read(byte[] message)
        {
            return NeonHelper.JsonDeserialize<Completion>(message);
        }
    }
}
