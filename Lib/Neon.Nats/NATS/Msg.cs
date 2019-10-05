//-----------------------------------------------------------------------------
// FILE:	    Msg.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.
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
    public class Msg<TMessage>
        where TMessage : class, IRoundtripData, new()
    {
        private string          subject;
        private string          reply;
        private TMessage        data;
        private ISubscription   sub;
        private Msg             cached;

        /// <summary>
        /// Default constructor.
        /// </summary>
        public Msg()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Msg"/> class with a subject, reply, and data.
        /// </summary>
        /// <param name="subject">Subject of the message.</param>
        /// <param name="reply">A reply subject, or <c>null</c>.</param>
        /// <param name="data">The message payload or <c>null</c>.</param>
        public Msg(string subject, string reply, TMessage data)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrWhiteSpace(subject), nameof(subject));

            this.subject = subject;
            this.reply    = reply;
            this.data     = data;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Msg"/> class with a subject and data.
        /// </summary>
        /// <param name="subject">Subject of the message.</param>
        /// <param name="data">The message payload or <c>null</c>.</param>
        public Msg(string subject, TMessage data)
            : this(subject, null, data)
        {
        }
        
        /// <summary>
        /// Initializes a new instance of the <see cref="Msg"/> class with a subject and no data.
        /// </summary>
        /// <param name="subject">Subject of the message.</param>
        public Msg(string subject)
            : this(subject, null, null)
        {
        }

        /// <summary>
        /// Constructs an instance from a low-level <see cref="Msg"/>.
        /// </summary>
        /// <param name="msg">The low-level message.</param>
        internal Msg(Msg msg)
        {
            Covenant.Requires<ArgumentNullException>(msg != null, nameof(msg));

            this.subject = msg.Subject;
            this.reply   = msg.Reply;
            this.data    = RoundtripDataFactory.CreateFrom<TMessage>(msg.Data);
            this.sub     = msg.ArrivalSubcription;
            this.cached  = msg;
        }

        /// <summary>
        /// Gets or sets the subject.
        /// </summary>
        public string Subject
        {
            get { return subject; }

            set
            {
                subject = value;

                if (cached != null)
                {
                    cached.Subject = value;
                }
            }
        }

        /// <summary>
        /// Gets or sets the reply subject.
        /// </summary>
        public string Reply
        {
            get { return reply; }

            set
            {
                reply = value;

                if (cached != null)
                {
                    cached.Reply = value;
                }
            }
        }

        /// <summary>
        /// Gets or sets the payload of the message.
        /// </summary>
        public TMessage Data
        {
            get { return data; }

            set
            {
                data = value;

                if (cached != null)
                {
                    if (value != null)
                    {
                        cached.Data = value.ToBytes();
                    }
                    else
                    {
                        cached.Data = null;
                    }
                }
            }
        }

        /// <summary>
        /// Returns the <see cref="ISubscription"/> which received the message.
        /// </summary>
        public ISubscription ArrivalSubscription
        {
            get { return sub; }
            internal set { sub = value; }
        }

        /// <summary>
        /// Converts the instance into a basic <see cref="Msg"/>.
        /// </summary>
        /// <returns>The <see cref="Msg"/>.</returns>
        internal Msg ToBaseMsg()
        {
            if (cached == null)
            {
                cached = new Msg(subject, reply, null);

                if (data != null)
                {
                    cached.Data = data.ToBytes();
                }
            }

            return cached;
        }

        /// <summary>
        /// Generates a string representation of the messages.
        /// </summary>
        /// <returns>A string representation of the messages.</returns>
        public override string ToString()
        {
            return ToBaseMsg().ToString();
        }
    }
}
