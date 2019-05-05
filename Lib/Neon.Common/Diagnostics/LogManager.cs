//-----------------------------------------------------------------------------
// FILE:	    LogManager.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.
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

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Neon.Common;

// $todo(jeff.lill):
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
// unforunate hardcoding.
//
// I'm going to revisit this when I start implementing unit tests with dependency 
// injection.

namespace Neon.Diagnostics
{
    /// <summary>
    /// Global class used to manage application logging.
    /// </summary>
    public class LogManager : ILogManager
    {
        //---------------------------------------------------------------------
        // Static members

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

        private Dictionary<string, INeonLogger> nameToLogger = new Dictionary<string, INeonLogger>();
        private LogLevel                        logLevel     = LogLevel.Info;
        private TextWriter                      writer       = null;

        // $todo(jeff.lill)
        //
        // Using [nameToLogger] to implement thread safety via a [Monitor] may introduce
        // some performance overhead for ASP.NET sites with lots of traffic.  It
        // may be worth investigating whether a [SpinLock] might be better or perhaps
        // even reimplementing this using a concurrent collection.

        /// <summary>
        /// Default constructor.
        /// </summary>
        /// <param name="parseLogLevel">Indicates that the <b>LOG-LEVEL</b> environment variable should be parsed (defaults to <c>true</c>).</param>
        /// <param name="writer">Optionally specifies the output writer.  This defaults to <see cref="Console.Error"/>.</param>
        public LogManager(bool parseLogLevel = true, TextWriter writer = null)
        {
            if (parseLogLevel && !Enum.TryParse<LogLevel>(Environment.GetEnvironmentVariable("LOG_LEVEL"), true, out logLevel))
            {
                logLevel = LogLevel.Info;
            }

            this.writer = writer;
        }

        /// <summary>
        /// Specifies the level of events to be actually recorded.
        /// </summary>
        public LogLevel LogLevel { get; set; }

        /// <summary>
        /// Sets the log level by safely parsing a string.
        /// </summary>
        /// <param name="level">The level string or <c>null</c>.</param>
        /// <remarks>
        /// <para>
        /// This method recognizes the following case insensitive values: <b>CRITICAL</b>,
        /// <b>SERROR</b>, <b>ERROR</b>, <b>WARN</b>, <b>WARNING</b>, <b>INFO</b>, <b>SINFO</b>,
        /// <b>INFORMATION</b>, <b>DEBUG</b>, or <b>NONE</b>.
        /// </para>
        /// <note>
        /// <b>INFO</b> will be assumed if the parameter doesn't match any of the
        /// values listed above.
        /// </note>
        /// </remarks>
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

        /// <summary>
        /// Controls whether timestamps are emitted.  This defaults to <c>true</c>.
        /// </summary>
        public bool EmitTimestamp { get; set; } = true;

        /// <summary>
        /// Controls whether the <b>index</b> field is emitted.  This is a counter start
        /// starts at zero for each application instance and is incremented for each event 
        /// emitted to help reconstruct exactly what happened when the system time resolution
        /// isn't fine enough.  This defaults to <c>true</c>.
        /// </summary>
        public bool EmitIndex { get; set; } = true;

        /// <summary>
        /// Returns the logger for the existing name.
        /// </summary>
        /// <param name="name">The case sensitive logger name.</param>
        /// <param name="writer">Optionally specifies the output writer.  This defaults to <see cref="Console.Error"/>.</param>
        /// <returns>The <see cref="INeonLogger"/> instance.</returns>
        private INeonLogger InternalGetLogger(string name, TextWriter writer = null)
        {
            name = name ?? string.Empty;

            lock (nameToLogger)
            {
                if (!nameToLogger.TryGetValue(name, out var logger))
                {
                    logger = new NeonLogger(this, name, writer: writer);
                    nameToLogger.Add(name, logger);
                }

                return logger;
            }
        }

        /// <inheritdoc/>
        public INeonLogger GetLogger(string sourceModule = null)
        {
            return InternalGetLogger(sourceModule, writer);
        }

        /// <inheritdoc/>
        public INeonLogger GetLogger(Type type)
        {
            return InternalGetLogger(type.FullName, writer);
        }

        /// <inheritdoc/>
        public INeonLogger GetLogger<T>()
        {
            return InternalGetLogger(typeof(T).FullName, writer);
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

        /// <inheritdoc/>
        public ILogger CreateLogger(string sourceModule)
        {
            return (ILogger)GetLogger(sourceModule);
        }
    }
}
