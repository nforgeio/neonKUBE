//-----------------------------------------------------------------------------
// FILE:	    StanHelper.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2021 by neonFORGE LLC.  All rights reserved.
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
using System.Reflection;
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
    /// Internal helper methods.
    /// </summary>
    internal static class StanHelper
    {
        private static ConstructorInfo  stanBadSubscriptionExceptionConstructor;
        private static ConstructorInfo  stanMsgConstructor;
        private static MethodInfo       subscriptionManualAckMethod;

        /// <summary>
        /// Static constructor.
        /// </summary>
        static StanHelper()
        {
            var type = typeof(StanBadSubscriptionException);

            stanBadSubscriptionExceptionConstructor = type.GetConstructor(BindingFlags.Static | BindingFlags.NonPublic, null, Type.EmptyTypes, null);

            type = typeof(StanMsg);

            stanMsgConstructor = type.GetConstructor(BindingFlags.Static | BindingFlags.NonPublic, null, new Type[] { typeof(MsgProto), typeof(AsyncSubscription) }, null);

            type = typeof(IAsyncSubscription);

            subscriptionManualAckMethod = type.GetMethod("manualAck", BindingFlags.Instance | BindingFlags.NonPublic, null, new Type[] { typeof(StanMsg) }, null);
        }

        /// <summary>
        /// Uses reflection to create an instance of <see cref="StanBadSubscriptionException"/>
        /// using the <c>internal</c> constructor.
        /// </summary>
        /// <returns>A new <see cref="StanBadSubscriptionException"/>.</returns>
        internal static StanBadSubscriptionException NewStanBadSubscriptionException()
        {
            return (StanBadSubscriptionException)stanBadSubscriptionExceptionConstructor.Invoke(null);
        }

        /// <summary>
        /// Uses reflection to to construct a new <see cref="StanMsg"/>.
        /// </summary>
        /// <param name="proto">The message including protocol information.</param>
        /// <param name="sub">The subscription.</param>
        internal static StanMsg NewStanMsg(MsgProto proto, object sub)
        {
            return (StanMsg)stanMsgConstructor.Invoke(new object[] { proto, sub });
        }

        /// <summary>
        /// Uses reflection to call the <c>internal IAsyncSubscription.manualAck(StanMsg)</c> method.
        /// </summary>
        /// <param name="subscription">The subscription.</param>
        /// <param name="msg">The message being acknowledged.</param>
        internal static void ManualAck(this object subscription, StanMsg msg)
        {
            subscriptionManualAckMethod.Invoke(subscription, new object[] { msg });
        }
    }
}
