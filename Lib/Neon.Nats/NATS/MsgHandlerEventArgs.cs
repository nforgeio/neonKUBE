//-----------------------------------------------------------------------------
// FILE:	    MsgHandlerEventArgs.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.
// COPYRIGHT:   Copyright (c) 2015-2018 The NATS Authors (method comments)
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
using System.Diagnostics.Contracts;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using NATS.Client;
using STAN.Client;

using Neon.Common;
using Neon.Data;
using Neon.Diagnostics;
using Neon.Net;

namespace NATS.Client
{
    /// <summary>
    /// Asynchronous message handler event arguments for typed messages.
    /// </summary>
    /// <typeparam name="TMessage">The message type.</typeparam>
    public class MsgHandlerEventArgs<TMessage>
        where TMessage : class, IRoundtripData, new()
    {
        /// <summary>
        /// Constructs an instance from a low-level message.
        /// </summary>
        /// <param name="msg">The low-level message.</param>
        internal MsgHandlerEventArgs(Msg msg)
        {
            this.Message = new Msg<TMessage>(msg);
        }

        /// <summary>
        /// Returns the received message.
        /// </summary>
        public Msg<TMessage> Message { get; private set; }
    }
}
