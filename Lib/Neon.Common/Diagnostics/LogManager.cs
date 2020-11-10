//-----------------------------------------------------------------------------
// FILE:	    LogManager.cs
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
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Neon.Common;
using YamlDotNet.Serialization;

// $todo(jefflill):
//
// The logging model is a little wonky, especially for dependency injection purists.
// [LogManager] exposes the static [Default] property that is a shortcut that returns
// the [ILogManager] service registered in [NeonHelper.ServiceContainer].  This isn't
// too horrible, but it does assume that applications are in fact using this global
// property.
//
// [NeonHelper.ServiceContainer] may be set to a custom value, but developers will
// need to implement [ServiceContainer] which is non-standard and is unlikely to be
// compatible with other dependency injection implementations.
//
// I got to this point because I started out with my own logging scheme which has
// somewhat different capabilites than is supported by the Microsoft logging abstractions
// and I still like my scheme.  I've started to unify/bridge the two schemes by having 
// [INeonLogger] implement [ILogger] and with [NeonLoggerShim] wrapping an [ILogger] 
// such that it behaves like an [INeonLogger].
//
// This is a bit of a mess, probably mostly for unit testing.  Right now this is
// a particular problem in the [NeonController] implementation which has some
// unfortunate hardcoding.
//
// I'm going to revisit this when I start implementing unit tests with dependency 
// injection.

namespace Neon.Diagnostics
{
    /// <summary>
    /// A reasonable default implementation of an application log manager.  See
    /// <see cref="ILogManager"/> for a description of how log managers work.
    /// </summary>
    public class LogManager : ILogManager
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Returns the <see cref="Regex"/> used for validating program version strings.
        /// </summary>
        public static Regex VersionRegex { get; private set; } = new Regex(@"[0-9a-zA-Z\.-_/]+");

        /// <summary>
        /// <para>
        /// The default <see cref="ILogManager"/> that can be used by applications that don't
        /// use dependency injection.  This defaults to an instance of <see cref="LogManager"/>
        /// but can be set to something else for unit tests or early in application startup.
        /// </para>
        /// <para>
        /// Applications that do use dependency injection can obtain this by default via
        /// <see cref="NeonHelper.ServiceContainer"/>.
        /// </para>
        /// </summary>
        public static ILogManager Default
        {
            get { return NeonHelper.ServiceContainer.GetService<ILogManager>(); }

            set
            {
                // Ensure that updates to the default manager will also be reflected in 
                // the dependency services so users won't be surprised.

                NeonHelper.ServiceContainer.AddSingleton<ILogManager>(value);
                NeonHelper.ServiceContainer.AddSingleton<ILoggerProvider>(value);
            }
        }

        /// <summary>
        /// Returns a <b>log-nothing</b> log manager.
        /// </summary>
        public static ILogManager Disabled { get; private set; }
        
        /// <summary>
        /// Static constructor.
        /// </summary>
        static LogManager()
        {
            Default  = new LogManager();
            Disabled = new LogManager(parseLogLevel: false)
            {
                LogLevel = LogLevel.None
            };
        }

        //---------------------------------------------------------------------
        // Instance members

        // $todo(jefflill):
        //
        // Using [syncRoot] to implement thread safety via a [Monitor] may introduce
        // some performance overhead for ASP.NET sites with lots of traffic.  It
        // may be worth investigating whether a [SpinLock] might be better.

        private readonly object                             syncRoot       = new object();
        private readonly Dictionary<string, INeonLogger>    moduleToLogger = new Dictionary<string, INeonLogger>();
        private readonly TextWriter                         writer         = null;

        private LogLevel                                    logLevel       = LogLevel.Info;
        private long                                        emitCount;
        private LoggerCreatorDelegate                       loggerCreator;

        /// <summary>
        /// Default constructor.
        /// </summary>
        /// <param name="parseLogLevel">Indicates that the <b>LOG-LEVEL</b> environment variable should be parsed (defaults to <c>true</c>).</param>
        /// <param name="version">
        /// Optionally specifies the semantic version of the current program.  This can be an somewhat string 
        /// arbitrary string that matches this regex: <b>"[0-9a-zA-Z\.-_/]+"</b>.  This defaults to <c>null</c>.
        /// </param>
        /// <param name="writer">Optionally specifies the output writer.  This defaults to <see cref="Console.Error"/>.</param>
        public LogManager(bool parseLogLevel = true, string version = null, TextWriter writer = null)
        {
            if (parseLogLevel && !Enum.TryParse<LogLevel>(Environment.GetEnvironmentVariable("LOG_LEVEL"), true, out logLevel))
            {
                logLevel = LogLevel.Info;
            }

            if (!string.IsNullOrEmpty(version) && VersionRegex.IsMatch(version))
            {
                this.Version = version;
            }
            else
            {
                this.Version = "unknown";
            }

            this.writer = writer;

            // $hack(jefflill):
            //
            // On Linux, we're going to initialize the [emitCount] to the index persisted to
            // the [/dev/shm/log-index] file if this is present and parsable.  This will 
            // align the .NET logging index with any event written via startup scripts.
            //
            //      https://github.com/nforgeio/neonKUBE/issues/578

            emitCount = 0;

            if (NeonHelper.IsLinux)
            {
                try
                {
                    emitCount = long.Parse(File.ReadAllText("/dev/shm/log-index").Trim());
                }
                catch
                {
                    // Ignore any exceptions; we'll just start the index at 0.
                }
            }
        }

        /// <inheritdoc/>
        public void Reset()
        {
            lock (syncRoot)
            {
                LoggerCreator = null;
                LogLevel      = LogLevel.Info;
                EmitIndex     = true;
                emitCount     = 0;

                moduleToLogger.Clear();
                TestLogger.ClearEvents();
            }
        }

        /// <inheritdoc/>
        public string Version { get; set; } = null;

        /// <inheritdoc/>
        public LogLevel LogLevel
        {
            get => this.logLevel;
            set => this.logLevel = value;
        }

        /// <inheritdoc/>
        public void SetLogLevel(string level)
        {
            level = level ?? "INFO";

            switch (level.ToUpperInvariant())
            {
                case "CRITICAL":

                    LogLevel = LogLevel.Critical;
                    break;

                case "SERROR":

                    LogLevel = LogLevel.SError;
                    break;

                case "ERROR":

                    LogLevel = LogLevel.Error;
                    break;

                case "WARN":
                case "WARNING":

                    LogLevel = LogLevel.Warn;
                    break;

                default:
                case "INFO":
                case "INFORMATION":

                    LogLevel = LogLevel.Info;
                    break;

                case "SINFO":

                    LogLevel = LogLevel.SInfo;
                    break;

                case "DEBUG":

                    LogLevel = LogLevel.Debug;
                    break;

                case "NONE":

                    LogLevel = LogLevel.None;
                    break;
            }
        }

        /// <inheritdoc/>
        public bool EmitTimestamp { get; set; } = true;

        /// <inheritdoc/>
        public bool EmitIndex { get; set; } = true;

        /// <inheritdoc/>
        public long GetNextEventIndex()
        {
            if (EmitIndex)
            {
                return Interlocked.Increment(ref emitCount);
            }
            else
            {
                return -1;
            }
        }

        /// <inheritdoc/>
        public LoggerCreatorDelegate LoggerCreator
        {
            get => this.loggerCreator;

            set
            {
                // We're going to clear any cached loggers so they will be recreated
                // using the new create function as necessary.

                lock (syncRoot)
                {
                    moduleToLogger.Clear();

                    this.loggerCreator = value;
                }
            }
        }

        /// <summary>
        /// Uses the <see cref="LoggerCreator"/> function to construct a logger for a specific 
        /// source module..
        /// </summary>
        /// <param name="module">The case sensitive logger event source module (defaults to <c>null</c>).</param>
        /// <param name="writer">Optionally specifies a target <see cref="TextWriter"/>.</param>
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
        private INeonLogger CreateLogger(string module, TextWriter writer, string contextId, Func<bool> isLogEnabledFunc)
        {
            if (LoggerCreator == null)
            {
                return new TextLogger(this, module, writer: writer, contextId: contextId, isLogEnabledFunc: isLogEnabledFunc);
            }
            else
            {
                return loggerCreator(this, module, writer: writer, contextId: contextId, isLogEnabledFunc: isLogEnabledFunc);
            }
        }

        /// <inheritdoc/>
        private INeonLogger InternalGetLogger(string module, TextWriter writer = null, string contextId = null, Func<bool> isLogEnabledFunc = null)
        {
            var moduleKey = module ?? string.Empty;

            lock (syncRoot)
            {
                if (!moduleToLogger.TryGetValue(moduleKey, out var logger))
                {
                    logger = CreateLogger(module, writer: writer, contextId: contextId, isLogEnabledFunc: isLogEnabledFunc);

                    moduleToLogger.Add(moduleKey, logger);
                }

                return logger;
            }
        }

        /// <inheritdoc/>
        public INeonLogger GetLogger(string module = null, string contextId = null, Func<bool> isLogEnabledFunc = null)
        {
            return InternalGetLogger(module, writer, contextId, isLogEnabledFunc);
        }

        /// <inheritdoc/>
        public INeonLogger GetLogger(Type type, string contextId = null, Func<bool> isLogEnabledFunc = null)
        {
            return InternalGetLogger(type.FullName, writer, contextId, isLogEnabledFunc);
        }

        /// <inheritdoc/>
        public INeonLogger GetLogger<T>(string contextId = null, Func<bool> isLogEnabledFunc = null)
        {
            return InternalGetLogger(typeof(T).FullName, writer, contextId, isLogEnabledFunc);
        }

        //---------------------------------------------------------------------
        // ILoggerProvider implementation

        /// <inheritdoc/>
        public void Dispose()
        {
            Dispose(true);
        }

        /// <summary>
        /// Releases all associated resources.
        /// </summary>
        /// <param name="disposing">Pass <c>true</c> if we're disposing, <c>false</c> if we're finalizing.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                GC.SuppressFinalize(this);

                // Actually dispose any disposable members here if we add
                // any in the future.
            }
        }

        /// <summary>
        /// Creates a logger.
        /// </summary>
        /// <param name="sourceModule">Identifies the source module.</param>
        /// <returns>The created <see cref="ILogger"/>.</returns>
        public ILogger CreateLogger(string sourceModule)
        {
            return (ILogger)GetLogger(sourceModule);
        }
    }
}
