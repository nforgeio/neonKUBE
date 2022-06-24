//-----------------------------------------------------------------------------
// FILE:	    Invokation.cs
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
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.AspNetCore.SignalR.Protocol;

using Neon.Common;

using MessagePack;

using Newtonsoft.Json;

namespace Neon.SignalR
{
    internal class Invokation
    {
        /// <summary>
        /// The optional invokation ID.
        /// </summary>
        [JsonProperty(PropertyName = "InvocationId", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string InvocationId { get; set; } = null;

        /// <summary>
        /// The method name.
        /// </summary>
        [JsonProperty(PropertyName = "MethodName", Required = Required.Always, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string MethodName { get; set; } = null;

        /// <summary>
        /// The method arguments.
        /// </summary>
        [JsonProperty(PropertyName = "Args", Required = Required.Always, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public object[] Args { get; set; } = null;

        /// <summary>
        /// The list of connection IDs that should not receive this message.
        /// </summary>
        [JsonProperty(PropertyName = "ExcludedConnectionIds", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public IReadOnlyList<string> ExcludedConnectionIds { get; set; } = null;

        /// <summary>
        /// The <see cref="InvocationMessage"/>
        /// </summary>
        [JsonProperty(PropertyName = "Message", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public InvocationMessage Message { get; set; } = null;

        /// <summary>
        /// The optional return channel.
        /// </summary>
        [JsonProperty(PropertyName = "ReturnChannel", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string ReturnChannel { get; set; } = null;

        /// <summary>
        /// Writes a <see cref="Invokation"/> to a byte[].
        /// </summary>
        /// <param name="methodName"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        public static byte[] Write(string methodName, object?[] args) =>
            Write(methodName: methodName, args: args, excludedConnectionIds: null);

        /// <summary>
        /// Writes a <see cref="Invokation"/> to a byte[].
        /// </summary>
        /// <param name="methodName"></param>
        /// <param name="args"></param>
        /// <param name="invocationId"></param>
        /// <param name="excludedConnectionIds"></param>
        /// <param name="returnChannel"></param>
        /// <returns></returns>
        public static byte[] Write(string methodName, object?[] args, string? invocationId = null,
                                IReadOnlyList<string>? excludedConnectionIds = null, string? returnChannel = null)
        {
            var invokation = new Invokation()
            {
                InvocationId          = invocationId,
                MethodName            = methodName,
                Args                  = args,
                ExcludedConnectionIds = excludedConnectionIds,
                ReturnChannel         = returnChannel
            };

            return NeonHelper.JsonSerializeToBytes(invokation);
        }

        /// <summary>
        /// Reads an <see cref="Invokation"/> from a byte array.
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        public static Invokation Read(byte[] message)
        {
            var s = Encoding.UTF8.GetString(message);
            var d = NeonHelper.JsonDeserialize<dynamic>(message);
            return NeonHelper.JsonDeserialize<Invokation>(message);
        }
    }
}
