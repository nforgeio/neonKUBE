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

namespace Neon.Diagnostics
{
    /// <summary>
    /// Global class used to manage application logging.
    /// </summary>
    public static class LogManager
    {
        private static bool         initialized = false;
        private static LogLevel     logLevel;

        /// <summary>
        /// Initializes the manager.
        /// </summary>
        private static void Initialize()
        {
            if (!Enum.TryParse<LogLevel>(Environment.GetEnvironmentVariable("LOG_LEVEL"), true, out logLevel))
            {
                logLevel = LogLevel.Info;
            }

            initialized = true;
        }

        /// <summary>
        /// Specifies the level of events to be actually recorded.
        /// </summary>
        public static LogLevel LogLevel
        {
            get
            {
                if (!initialized)
                {
                    Initialize();
                }

                return logLevel;
            }

            set
            {
                logLevel    = value;
                initialized = true;
            }
        }

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
        public static void SetLogLevel(string level)
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
        public static bool EmitTimestamp { get; set; } = true;

        /// <summary>
        /// Controls whether the <b>index</b> field is emitted.  This is a counter start
        /// starts at zero for each application instance and is incremented for each event 
        /// emitted to help reconstruct exactly what happened when the system time resolution
        /// isn't fine enough.  This defaults to <c>true</c>.
        /// </summary>
        public static bool EmitIndex { get; set; } = true;

        /// <summary>
        /// Returns a named logger.
        /// </summary>
        /// <param name="name">The logger name (defaults to <c>null</c>).</param>
        /// <returns>The <see cref="ILog"/> instance.</returns>
        public static ILog GetLogger(string name = null)
        {
            return new Logger(name ?? string.Empty);
        }

        /// <summary>
        /// Returns a logger to be associated with a specific type.  This method
        /// supports both <c>static</c> and normal types.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <returns>The <see cref="ILog"/> instance.</returns>
        public static ILog GetLogger(Type type)
        {
            return new Logger(type.FullName);
        }

        /// <summary>
        /// Returns a logger to be associated with a specific type.  This
        /// method works only for non-<c>static</c> types.
        /// </summary>
        /// <typeparam name="T">The type.</typeparam>
        /// <returns>The <see cref="ILog"/> instance.</returns>
        public static ILog GetLogger<T>()
        {
            return new Logger(typeof(T).FullName);
        }
    }
}
