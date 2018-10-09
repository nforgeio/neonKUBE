//-----------------------------------------------------------------------------
// FILE:	    HiveEasyMQLogProvider.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

using EasyNetQ;
using EasyNetQ.Logging;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using RabbitMQ;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

using Neon.Common;
using Neon.Diagnostics;

namespace Neon.HiveMQ
{
    /// <summary>
    /// Implements an EasyNetQ logger that wraps an <see cref="INeonLogger"/>.
    /// </summary>
    internal class HiveEasyMQLogProvider : ILogProvider
    {
        //---------------------------------------------------------------------
        // Private types

        /// <summary>
        /// Used to return a do-nothing <see cref="IDisposable"/> below.
        /// </summary>
        private sealed class DummyDisposable : IDisposable
        {
            public static DummyDisposable Instance = new DummyDisposable();

            /// <inheritdoc/>
            public void Dispose()
            {
            }
        }

        /// <summary>
        /// I lifted this from EasyNetQ to handle log formatting.
        /// </summary>
        private static class LogMessageFormatter
        {
            //private static readonly Regex Pattern = new Regex(@"\{@?\w{1,}\}");
#if LIBLOG_PORTABLE
        private static readonly Regex Pattern = new Regex(@"(?<!{){@?(?<arg>[^\d{][^ }]*)}");
#else
            private static readonly Regex Pattern = new Regex(@"(?<!{){@?(?<arg>[^ :{}]+)(?<format>:[^}]+)?}", RegexOptions.Compiled);
#endif

            /// <summary>
            /// Some logging frameworks support structured logging, such as serilog. This will allow you to add names to structured data in a format string:
            /// For example: Log("Log message to {user}", user). This only works with serilog, but as the user of LibLog, you don't know if serilog is actually 
            /// used. So, this class simulates that. it will replace any text in {curly braces} with an index number. 
            /// 
            /// "Log {message} to {user}" would turn into => "Log {0} to {1}". Then the format parameters are handled using regular .net string.Format.
            /// </summary>
            /// <param name="messageBuilder">The message builder.</param>
            /// <param name="formatParameters">The format parameters.</param>
            /// <returns></returns>
            public static Func<string> SimulateStructuredLogging(Func<string> messageBuilder, object[] formatParameters)
            {
                if (formatParameters == null || formatParameters.Length == 0)
                {
                    return messageBuilder;
                }

                return () =>
                {
                    string targetMessage = messageBuilder();
                    IEnumerable<string> patternMatches;
                    return FormatStructuredMessage(targetMessage, formatParameters, out patternMatches);
                };
            }

            private static string ReplaceFirst(string text, string search, string replace)
            {
                int pos = text.IndexOf(search, StringComparison.Ordinal);
                if (pos < 0)
                {
                    return text;
                }
                return text.Substring(0, pos) + replace + text.Substring(pos + search.Length);
            }

            public static string FormatStructuredMessage(string targetMessage, object[] formatParameters, out IEnumerable<string> patternMatches)
            {
                if (formatParameters.Length == 0)
                {
                    patternMatches = Enumerable.Empty<string>();
                    return targetMessage;
                }

                List<string> processedArguments = new List<string>();
                patternMatches = processedArguments;

                foreach (Match match in Pattern.Matches(targetMessage))
                {
                    var arg = match.Groups["arg"].Value;

                    int notUsed;
                    if (!int.TryParse(arg, out notUsed))
                    {
                        int argumentIndex = processedArguments.IndexOf(arg);
                        if (argumentIndex == -1)
                        {
                            argumentIndex = processedArguments.Count;
                            processedArguments.Add(arg);
                        }

                        targetMessage = ReplaceFirst(targetMessage, match.Value,
                            "{" + argumentIndex + match.Groups["format"].Value + "}");
                    }
                }
                try
                {
                    return string.Format(CultureInfo.InvariantCulture, targetMessage, formatParameters);
                }
                catch (FormatException ex)
                {
                    throw new FormatException("The input string '" + targetMessage + "' could not be formatted using string.Format", ex);
                }
            }
        }

        //---------------------------------------------------------------------
        // Implementation

        private INeonLogger     neonLogger;
        private Logger          loggerFunc;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="neonLogger">The Neon base logger.</param>
        public HiveEasyMQLogProvider(INeonLogger neonLogger)
        {
            Covenant.Requires<ArgumentNullException>(neonLogger != null);

            this.neonLogger = neonLogger;
            this.loggerFunc =
                (logLevel, messageFunc, exception, formatParameters) =>
                {
                    if (messageFunc == null)
                    {
                        return true;
                    }

                    var message = LogMessageFormatter.FormatStructuredMessage(messageFunc(), formatParameters, out _);

                    switch (logLevel)
                    {
                        case EasyNetQ.Logging.LogLevel.Trace:

                            // NOTE: Neon logging doesn't have a TRACE level so we'll
                            // map these to DEBUG.

                        case EasyNetQ.Logging.LogLevel.Debug:

                            if (neonLogger.IsDebugEnabled)
                            {
                                if (exception == null)
                                {
                                    neonLogger.LogDebug(message);
                                }
                                else
                                {
                                    neonLogger.LogDebug(message, exception);
                                }
                            }
                            break;

                        case EasyNetQ.Logging.LogLevel.Error:

                            if (neonLogger.IsErrorEnabled)
                            {
                                if (exception == null)
                                {
                                    neonLogger.LogError(message);
                                }
                                else
                                {
                                    neonLogger.LogError(message, exception);
                                }
                            }
                            break;

                        case EasyNetQ.Logging.LogLevel.Fatal:

                            if (neonLogger.IsCriticalEnabled)
                            {
                                if (exception == null)
                                {
                                    neonLogger.LogCritical(message);
                                }
                                else
                                {
                                    neonLogger.LogCritical(message, exception);
                                }
                            }
                            break;

                        case EasyNetQ.Logging.LogLevel.Info:

                            if (neonLogger.IsInfoEnabled)
                            {
                                if (exception == null)
                                {
                                    neonLogger.LogInfo(message);
                                }
                                else
                                {
                                    neonLogger.LogInfo(message, exception);
                                }
                            }
                            break;

                        case EasyNetQ.Logging.LogLevel.Warn:

                            if (neonLogger.IsWarnEnabled)
                            {
                                if (exception == null)
                                {
                                    neonLogger.LogWarn(message);
                                }
                                else
                                {
                                    neonLogger.LogWarn(message, exception);
                                }
                            }
                            break;
                    }

                    return true;
                };
        }

        /// <inheritdoc/>
        public Logger GetLogger(string name)
        {
            return loggerFunc;
        }

        /// <inheritdoc/>
        public IDisposable OpenMappedContext(string key, object value, bool destructure = false)
        {
            return DummyDisposable.Instance;
        }

        /// <inheritdoc/>
        public IDisposable OpenNestedContext(string message)
        {
            return DummyDisposable.Instance;
        }
    }
}
