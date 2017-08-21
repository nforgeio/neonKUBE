//-----------------------------------------------------------------------------
// FILE:	    LogManager.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2017 by NeonForge, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Neon.Common;

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
            }
        }
        
        /// <summary>
        /// Static constructor.
        /// </summary>
        static LogManager()
        {
            Default = new LogManager();
        }

        //---------------------------------------------------------------------
        // Instance members

        private Dictionary<string, ILog>    nameToLogger = new Dictionary<string, ILog>();
        private LogLevel                    logLevel     = LogLevel.Info;

        /// <summary>
        /// Default constructor.
        /// </summary>
        /// <param name="parseLogLevel">Indicates that the <b>LOG-LEVEL</b> environment variable should be parsed (defaults to <c>true</c>).</param>
        public LogManager(bool parseLogLevel = true)
        {
            if (parseLogLevel && !Enum.TryParse<LogLevel>(Environment.GetEnvironmentVariable("LOG_LEVEL"), true, out logLevel))
            {
                logLevel = LogLevel.Info;
            }
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
        /// This method recognizes the following case insenstive values: <b>CRITICAL</b>,
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
        /// <returns>The <see cref="ILog"/> instance.</returns>
        private ILog InternalGetLogger(string name)
        {
            name = name ?? string.Empty;

            lock (nameToLogger)
            {
                if (!nameToLogger.TryGetValue(name, out var logger))
                {
                    logger = new Logger(this, name);
                    nameToLogger.Add(name, logger);
                }

                return logger;
            }
        }

        /// <summary>
        /// Returns a named logger.
        /// </summary>
        /// <param name="name">The case sensitive logger name (defaults to <c>null</c>).</param>
        /// <returns>The <see cref="ILog"/> instance.</returns>
        public ILog GetLogger(string name = null)
        {
            return InternalGetLogger(name);
        }

        /// <summary>
        /// Returns a logger to be associated with a specific type.  This method
        /// supports both <c>static</c> and normal types.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <returns>The <see cref="ILog"/> instance.</returns>
        public ILog GetLogger(Type type)
        {
            return InternalGetLogger(type.FullName);
        }

        /// <summary>
        /// Returns a logger to be associated with a specific type.  This
        /// method works only for non-<c>static</c> types.
        /// </summary>
        /// <typeparam name="T">The type.</typeparam>
        /// <returns>The <see cref="ILog"/> instance.</returns>
        public ILog GetLogger<T>()
        {
            return InternalGetLogger(typeof(T).FullName);
        }

        //---------------------------------------------------------------------
        // ILoggerProvider implementation

        /// <inheritdoc/>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases all associated resources.
        /// </summary>
        /// <param name="disposing">Pass <c>true</c> if we're disposing, <c>false</c> if we're finalizing.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Actually dispose any disposable members here if we add
                // any in the future.
            }
        }

        /// <inheritdoc/>
        public ILogger CreateLogger(string categoryName)
        {
            return (ILogger)GetLogger(categoryName);
        }
    }
}
