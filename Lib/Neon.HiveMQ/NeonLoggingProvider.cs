//-----------------------------------------------------------------------------
// FILE:	    NeonLoggingProvider.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Contracts;

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
    internal class NeonLoggingProvider : ILogProvider
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

        //---------------------------------------------------------------------
        // Implementation

        private INeonLogger     neonLogger;
        private Logger          loggerFunc;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="neonLogger">The Neon base logger.</param>
        public NeonLoggingProvider(INeonLogger neonLogger)
        {
            Covenant.Requires<ArgumentNullException>(neonLogger != null);

            this.neonLogger = neonLogger;
            this.loggerFunc =
                (logLevel, messageFunc, exception, formatParameters) =>
                {
                    var message = messageFunc() ?? string.Empty;

                    switch (logLevel)
                    {
                        case EasyNetQ.Logging.LogLevel.Debug:
                        case EasyNetQ.Logging.LogLevel.Trace:

                            // NOTE: Neon logging doesn't have a TRACE level so we'll
                            // map these to DEBUG.

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

                    // Nothing was logged if we reach this point.

                    return false;
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
