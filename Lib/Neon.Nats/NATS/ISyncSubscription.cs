//-----------------------------------------------------------------------------
// FILE:	    ISyncSubscription.cs
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
    /// Implements an <see cref="ISyncSubscription"/> for typed messages.
    /// </summary>
    /// <typeparam name="TMessage">The message type.</typeparam>
    public interface ISyncSubscription<TMessage> : ISubscription, IDisposable
        where TMessage : class, IRoundtripData, new()
    {
        /// <summary>
        /// Returns the next <see cref="Msg{TMessage}"/> available to a synchronous
        /// subscriber, blocking until one is available.
        /// </summary>
        /// <returns>The next <see cref="Msg{TMessage}"/> available to a subscriber.</returns>
        Msg<TMessage> NextMessage();

        /// <summary>
        /// Returns the next <see cref="Msg{TMessage}"/> available to a synchronous
        /// subscriber, or block up to a given timeout until the next one is available.
        /// </summary>
        /// <param name="timeout">The amount of time, in milliseconds, to wait for
        /// the next message.</param>
        /// <returns>The next <see cref="Msg{TMessage}"/> available to a subscriber.</returns>
        Msg<TMessage> NextMessage(int timeout);
    }
}
