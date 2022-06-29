//-----------------------------------------------------------------------------
// FILE:	    Log.cs
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
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

namespace Neon.Web.SignalR
{
    internal static partial class Log
    {
        [LoggerMessage(2, LogLevel.Information, "Connected to NATS.", EventName = "Connected")]
        public static partial void Connected(ILogger logger);

        [LoggerMessage(3, LogLevel.Trace, "Subscribing to subject: {Subject}.", EventName = "Subscribing")]
        public static partial void Subscribing(ILogger logger, string subject);

        [LoggerMessage(3, LogLevel.Trace, "Subscribing to subject: {Subject} Failed", EventName = "SubscribingFailed")]
        public static partial void SubscribingFailed(ILogger logger, string subject);

        [LoggerMessage(4, LogLevel.Trace, "Received message from NATS subject {Subject}.", EventName = "ReceivedFromSubject")]
        public static partial void ReceivedFromSubject(ILogger logger, string subject);

        [LoggerMessage(5, LogLevel.Trace, "Publishing message to NATS subject {Subject}.", EventName = "PublishToSubject")]
        public static partial void PublishToSubject(ILogger logger, string subject);

        [LoggerMessage(6, LogLevel.Trace, "Unsubscribing from subject: {Subject}.", EventName = "Unsubscribe")]
        public static partial void Unsubscribe(ILogger logger, string subject);

        [LoggerMessage(6, LogLevel.Debug, "Unsubscribing from subject: {Subject} Failed.", EventName = "UnsubscribeFailed")]
        public static partial void UnsubscribeFailed(ILogger logger, string subject);

        [LoggerMessage(7, LogLevel.Error, "Not connected to NATS.", EventName = "Connected")]
        public static partial void NotConnected(ILogger logger);

        [LoggerMessage(8, LogLevel.Information, "Connection to NATS restored.", EventName = "ConnectionRestored")]
        public static partial void ConnectionRestored(ILogger logger);

        [LoggerMessage(9, LogLevel.Error, "Connection to NATS failed.", EventName = "ConnectionFailed")]
        public static partial void ConnectionFailed(ILogger logger, Exception exception);

        [LoggerMessage(10, LogLevel.Debug, "Failed writing message.", EventName = "FailedWritingMessage")]
        public static partial void FailedWritingMessage(ILogger logger, Exception exception);

        [LoggerMessage(10, LogLevel.Debug, "Timed out waiting for ACK.", EventName = "AckTimedOut")]
        public static partial void AckTimedOut(ILogger logger, Exception exception);

        [LoggerMessage(11, LogLevel.Warning, "Error processing message for internal server message.", EventName = "InternalMessageFailed")]
        public static partial void InternalMessageFailed(ILogger logger, Exception exception);

        [LoggerMessage(12, LogLevel.Error, "Received a client result for protocol {HubProtocol} which is not supported by this server. This likely means you have different versions of your server deployed.", EventName = "MismatchedServers")]
        public static partial void MismatchedServers(ILogger logger, string hubProtocol);

        [LoggerMessage(13, LogLevel.Error, "Error forwarding client result with ID '{InvocationID}' to server.", EventName = "ErrorForwardingResult")]
        public static partial void ErrorForwardingResult(ILogger logger, string invocationId, Exception ex);

        [LoggerMessage(14, LogLevel.Error, "Error connecting to NATS.", EventName = "ErrorConnecting")]
        public static partial void ErrorConnecting(ILogger logger, Exception ex);
    }
}
