//-----------------------------------------------------------------------------
// FILE:	    StanMsg.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2020 by neonFORGE, LLC.  All rights reserved.
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

using NATS.Client;
using STAN.Client;

using Neon.Common;
using Neon.Data;
using Neon.Diagnostics;
using Neon.Net;

namespace NATS.Client
{
    /// <summary>
    /// A NATS message encapsulating a subject, optional reply
    /// payload, and subscription information, sent or received by the client
    /// application.
    /// </summary>
    /// <typeparam name="TMessage">The request message type.</typeparam>
    public class StanMsg<TMessage>
        where TMessage : class, IRoundtripData, new()
    {
        private MsgProto    proto;
        private object      sub;
        private TMessage    cached;

        /// <summary>
        /// Internal constructor.
        /// </summary>
        /// <param name="proto">The message including protocol information.</param>
        /// <param name="subscription">The subscription.</param>
        internal StanMsg(MsgProto proto, object subscription)
        {
            Covenant.Requires<ArgumentNullException>(proto != null, nameof(proto));

            this.proto = proto;
            this.sub   = subscription;
        }

        /// <summary>
        /// Returns the message time stamp as Unix nanotime.
        /// </summary>
        public long Time => proto.Timestamp;

        /// <summary>
        /// Returns the message time stamp as a <see cref="DateTime"/> (UTC).
        /// </summary>
        public DateTime TimeStamp => new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc).AddTicks(proto.Timestamp/100);

        /// <summary>
        /// Acknowledge a message.
        /// </summary>
        public void Ack()
        {
            if (sub == null)
            {
                throw StanHelper.NewStanBadSubscriptionException();
            }

            StanHelper.ManualAck(sub, StanHelper.NewStanMsg(proto, sub));
        }

        /// <summary>
        /// Gets the sequence number of a message.
        /// </summary>
        public ulong Sequence => proto.Sequence;

        /// <summary>
        /// Gets the subject of the message.
        /// </summary>
        public string Subject => proto.Subject;

        /// <summary>
        /// Returns the message payload.
        /// </summary>
        public TMessage Data
        {
            get
            {
                if (cached != null)
                {
                    return cached;
                }
                else if (proto.Data == null)
                {
                    return null;
                }

                return cached = RoundtripDataFactory .CreateFrom<TMessage>(proto.Data.ToByteArray());
            }
        }

        /// <summary>
        /// The redelivered property if true if this message has been redelivered, false otherwise.
        /// </summary>
        public bool Redelivered => proto.Redelivered;

        /// <summary>
        /// Gets the subscription this message was received from.
        /// </summary>
        public IStanSubscription Subscription => (IStanSubscription)sub;
    }
}
