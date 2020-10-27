//-----------------------------------------------------------------------------
// FILE:	    ILogManager.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2020 by neonFORGE LLC.  All rights reserved.
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
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

namespace Neon.Diagnostics
{
    /// <summary>
    /// Defines the <see cref="ILogManager.LoggerCreator"/> function used to return custom
    /// logger implementations.
    /// </summary>
    /// <param name="logManager">The parent log manager.</param>
    /// <param name="module">The case sensitive logger event source module (defaults to <c>null</c>).</param>
    /// <param name="writer">
    /// Optionally specifies a target <see cref="TextWriter"/>.  This can be useful for 
    /// redirecting the ourput of a text logger to a file or somewhere else.  This parameter
    /// may be ignored for non-text based loggers or for other logger specific reasons.
    /// </param>
    /// <param name="contextId">
    /// Optionally specifies additional information that can be used to identify
    /// context for logged events.  For example, the Neon.Cadence client uses this 
    ///  to record the ID of the workflow recording events.
    /// </param>
    /// <param name="isLogEnabledFunc">
    /// Optionally specifies a function that will be called at runtime to
    /// determine whether to actually log an event.  This defaults to <c>null</c>
    /// which will always log events.
    /// </param>
    /// <returns>The <see cref="INeonLogger"/> instance.</returns>
    public delegate INeonLogger LoggerCreatorDelegate(LogManager logManager, string module, TextWriter writer, string contextId, Func<bool> isLogEnabledFunc);

    /// <summary>
    /// Describes an application log manager implementation.  <see cref="LogManager"/> is a reasonable
    /// implementation for many situations but it's possible for developers to implement custom solutions.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Neon log managers are used to manage one or more <see cref="INeonLogger"/> instances that handle
    /// logging for parts of your application, we refer to as <b>modules</b>.  By convention, a module 
    /// is often the fully qualified name of the .NET type doing the logging but this is just a string
    /// and can be set to anything.  Log managers are responsible for mapping modules to loggers, potentially
    /// caching these loggers for performance, and then submitting events to these loggers to be recorded.
    /// </para>
    /// <para>
    /// Log managers also maintain a <c>long</c> count of the events emitted by the application.  This counter 
    /// is used to record the index of the event in the stream of application events.  This index is typically
    /// one-based and is useful for knowing the strict order that events were actually recorded.  Event
    /// timestamps often don't have enough resolution accomplish this.
    /// </para>
    /// <para>
    /// Log managers provide the <see cref="LogLevel"/> property which can be used to control which events
    /// are actually recorded.  <see cref="Neon.Diagnostics.LogLevel"/> for information about the relative 
    /// lof levels.
    /// </para>
    /// <para>
    /// Log managers typically provide a default <see cref="INeonLogger"/> implementation.  <see cref="LogManager"/>
    /// defaults to logging events to STDERR as text via <see cref="TextLogger"/> which is suitable for
    /// many server applications, espectially for those deployed as containers where this is standard
    /// behavior.  <see cref="LogManager"/> also implements the <see cref="LoggerCreator"/> delegate 
    /// as an easy way to support custom loggers in your application without having to implement a custom
    /// <see cref="ILogManager"/> as well.
    /// </para>
    /// </remarks>
    public interface ILogManager : ILoggerProvider
    {
        /// <summary>
        /// Intended to reset the log manager to its default condition.  Implementations may cxustomize
        /// what this actually does but the default <see cref="LogManager"/> implementation resets its
        /// emitted event counter to zewro, clears and cached loggers, and resets the <see cref="LoggerCreator"/>
        /// delegate.
        /// </summary>
        void Reset();

        /// <summary>
        /// Specifies the level of events to be actually recorded.
        /// </summary>
        LogLevel LogLevel { get; set; }

        /// <summary>
        /// Sets the log level by safely parsing a string.
        /// </summary>
        /// <param name="level">The level string or <c>null</c>.</param>
        /// <remarks>
        /// <para>
        /// This method recognizes the following case insensitive values: <b>CRITICAL</b>,
        /// <b>SERROR</b>, <b>ERROR</b>, <b>WARN</b>, <b>WARNING</b>, <b>INFO</b>, <b>SINFO</b>,
        /// <b>INFORMATION</b>, <b>TRANSIENT</b>, <b>DEBUG</b>, or <b>NONE</b>.
        /// </para>
        /// <note>
        /// <b>INFO</b> will be assumed if the parameter doesn't match any of the
        /// values listed above.
        /// </note>
        /// </remarks>
        void SetLogLevel(string level);

        /// <summary>
        /// Controls whether timestamps are emitted.  This defaults to <c>true</c>.
        /// </summary>
        bool EmitTimestamp { get; set; }

        /// <summary>
        /// Controls whether the <b>index</b> field is emitted.  This is a counter start
        /// starts at zero for each application instance and is incremented for each event 
        /// emitted to help reconstruct exactly what happened when the system time resolution
        /// isn't fine enough.  This defaults to <c>true</c>.
        /// </summary>
        bool EmitIndex { get; set; }

        /// <summary>
        /// Returns the next event index.
        /// </summary>
        /// <returns>The event index or -1 if <see cref="EmitIndex"/><c> = false</c>.</returns>
        long GetNextEventIndex();

        /// <summary>
        /// Used to customize what type of <see cref="INeonLogger"/> will be returned by the 
        /// various <see cref="GetLogger(string, string, Func{bool})"/> methods.  This defaults
        /// to creating <see cref="TextLogger"/> instances.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <see cref="ILogManager"/> implementations can choose to ignore this delegate when
        /// creating loggers.  The default <see cref="LogManager"/> implementation does honor
        /// this as an easy for applications to change the loggers without having to go to
        /// the trouble of implementing and registering an new <see cref="ILogManager"/>
        /// implementation.
        /// </para>
        /// </remarks>
        LoggerCreatorDelegate LoggerCreator { get; set; }

        /// <summary>
        /// Returns a named logger.
        /// </summary>
        /// <param name="module">The case sensitive logger event source module (defaults to <c>null</c>).</param>
        /// <param name="contextId">
        /// Optionally specifies additional information that can be used to identify
        /// context for logged events.  For example, the Neon.Cadence client uses this 
        ///  to record the ID of the workflow recording events.
        /// </param>
        /// <param name="isLogEnabledFunc">
        /// Optionally specifies a function that will be called at runtime to
        /// determine whether to actually log an event.  This defaults to <c>null</c>
        /// which will always log events.
        /// </param>
        /// <returns>The <see cref="INeonLogger"/> instance.</returns>
        INeonLogger GetLogger(string module = null, string contextId = null, Func<bool> isLogEnabledFunc = null);

        /// <summary>
        /// Returns a logger to be associated with a specific type.  This method
        /// supports both <c>static</c> and normal types.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <param name="contextId">
        /// Optionally specifies additional information that can be used to identify
        /// context for logged events.  For example, the Neon.Cadence client uses this 
        ///  to record the ID of the workflow recording events.
        /// </param>
        /// <param name="isLogEnabledFunc">
        /// Optionally specifies a function that will be called at runtime to
        /// determine whether to actually log an event.  This defaults to <c>null</c>
        /// which will always log events.
        /// </param>
        /// <returns>The <see cref="INeonLogger"/> instance.</returns>
        INeonLogger GetLogger(Type type, string contextId = null, Func<bool> isLogEnabledFunc = null);

        /// <summary>
        /// Returns a logger to be associated with a specific type.  This
        /// method works only for non-<c>static</c> types.
        /// </summary>
        /// <typeparam name="T">The type.</typeparam>
        /// <param name="contextId">
        /// Optionally specifies additional information that can be used to identify
        /// context for logged events.  For example, the Neon.Cadence client uses this 
        ///  to record the ID of the workflow recording events.
        /// </param>
        /// <param name="isLogEnabledFunc">
        /// Optionally specifies a function that will be called at runtime to
        /// determine whether to actually log an event.  This defaults to <c>null</c>
        /// which will always log events.
        /// </param>
        /// <returns>The <see cref="INeonLogger"/> instance.</returns>
        INeonLogger GetLogger<T>(string contextId = null, Func<bool> isLogEnabledFunc = null);
    }
}
