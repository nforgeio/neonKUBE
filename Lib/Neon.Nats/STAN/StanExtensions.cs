//-----------------------------------------------------------------------------
// FILE:	    StanExtensions.cs
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
using System.Threading.Tasks;

using NATS.Client;
using STAN.Client;

using Neon.Common;
using Neon.Data;
using Neon.Diagnostics;
using Neon.Net;

namespace STAN.Client
{
    /// <summary>
    /// Implements handy STAN extension methods.
    /// </summary>
    public static class StanExtensions
    {
        //---------------------------------------------------------------------
        // IStanConnection extensions

        /// <summary>
        /// Publish publishes the data argument to the given subject. The data
        /// argument is left untouched and needs to be correctly interpreted on
        /// the receiver.  This API is synchronous and waits for the acknowledgement
        /// or error from the NATS streaming server.
        /// </summary>
        /// <typeparam name="TMessage">The message type.</typeparam>
        /// <param name="subject">Subject to publish the message to.</param>
        /// <param name="data">Message payload.</param>
        /// <exception cref="StanException">When an error occurs locally or on the NATS streaming server.</exception>
	    public static void Publish<TMessage>(string subject, TMessage data)
            where TMessage : class, IGeneratedType, new()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Publish publishes the data argument to the given subject. The data
        /// argument is left untouched and needs to be correctly interpreted on
        /// the receiver.  This API is asynchronous and handles the acknowledgement
        /// or error from the NATS streaming server in the provided handler.  An exception is thrown when
        /// an error occurs during the send, the handler will process acknowledgments and errors.
        /// </summary>
        /// <typeparam name="TMessage">The message type.</typeparam>
        /// <param name="subject">Subject to publish the message to.</param>
        /// <param name="data">Message payload.</param>
        /// <param name="handler">Event handler to process message acknowledgements.</param>
        /// <returns>The GUID of the published message.</returns>
        /// <exception cref="StanException">Thrown when an error occurs publishing the message.</exception>
        public static string Publish<TMessage>(string subject, TMessage data, EventHandler<StanAckHandlerArgs> handler)
            where TMessage : class, IGeneratedType, new()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Publish publishes the data argument to the given subject. The data
        /// argument is left untouched and needs to be correctly interpreted on
        /// the receiver.  This API is asynchronous and handles the acknowledgement
        /// or error from the NATS streaming server in the provided handler.  An exception is thrown when
        /// an error occurs during the send, the handler will process acknowledgments and errors.
        /// </summary>
        /// <typeparam name="TMessage">The message type.</typeparam>
        /// <param name="subject">Subject to publish the message to.</param>
        /// <param name="data"></param>
        /// <returns>The task object representing the asynchronous operation, containing the guid.</returns>
        public static Task<string> PublishAsync<TMessage>(string subject, TMessage data)
            where TMessage : class, IGeneratedType, new()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Subscribe will create an Asynchronous Subscriber with
        /// interest in a given subject, assign the handler, and immediately
        /// start receiving messages.  The subscriber will default options.
        /// </summary>
        /// <typeparam name="TMessage">The message type.</typeparam>
        /// <param name="subject">Subject of interest.</param>
        /// <param name="handler">A message handler to process messages.</param>
        /// <returns>A new Subscription</returns>
        /// <exception cref="StanException">An error occured creating the subscriber.</exception>
	    public static IStanSubscription Subscribe<TMessage>(string subject, EventHandler<StanMsgHandlerArgs<TMessage>> handler)
            where TMessage : class, IGeneratedType, new()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Subscribe will create an Asynchronous subscriber with
        /// interest in a given subject, assign the handler, and immediately
        /// start receiving messages.
        /// </summary>
        /// <typeparam name="TMessage">The message type.</typeparam>
        /// <param name="subject">Subject of interest.</param>
        /// <param name="options">SubscriptionOptions used to create the subscriber.</param>
        /// <param name="handler">A message handler to process messages.</param>
        /// <returns>A new subscription.</returns>
        /// <exception cref="StanException">An error occured creating the subscriber.</exception>
        public static IStanSubscription Subscribe<TMessage>(string subject, StanSubscriptionOptions options, EventHandler<StanMsgHandlerArgs<TMessage>> handler)
            where TMessage : class, IGeneratedType, new()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Subscribe will create an Asynchronous Subscriber with
        /// interest in a given subject, assign the handler, and immediately
        /// start receiving messages.  The subscriber will use default 
        /// subscriber options.
        /// </summary>
        /// <typeparam name="TMessage">The message type.</typeparam>
        /// <param name="subject">Subject of interest.</param>
        /// <param name="qgroup">Name of the queue group.</param>
        /// <param name="handler">A message handler to process messages.</param>
        /// <returns>A new subscription.</returns>
        public static IStanSubscription Subscribe<TMessage>(string subject, string qgroup, EventHandler<StanMsgHandlerArgs<TMessage>> handler)
            where TMessage : class, IGeneratedType, new()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Subscribe will create an Asynchronous Subscriber with
        /// interest in a given subject, assign the handler, and immediately
        /// start receiving messages.
        /// </summary>
        /// <typeparam name="TMessage">The message type.</typeparam>
        /// <param name="subject">Subject of interest.</param>
        /// <param name="qgroup">Name of the queue group.</param>
        /// <param name="options">SubscriptionOptions used to create the subscriber.</param>
        /// <param name="handler">A message handler to process messages.</param>
        /// <returns>A new subscription.</returns>
        public static IStanSubscription Subscribe<TMessage>(string subject, string qgroup, StanSubscriptionOptions options, EventHandler<StanMsgHandlerArgs<TMessage>> handler)
            where TMessage : class, IGeneratedType, new()
        {
            throw new NotImplementedException();
        }
    }
}
