//-----------------------------------------------------------------------------
// FILE:	    LogLevel.cs
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
    /// Enumerates the possible log levels.
    /// </summary>
    public enum LogLevel
    {
        /// <summary>
        /// Logging is disabled.
        /// </summary>
        None,

        /// <summary>
        /// A critical or fatal error has been detected.
        /// </summary>
        Critical,

        /// <summary>
        /// An error has been detected. 
        /// </summary>
        Error,

        /// <summary>
        /// An unusual condition has been detected that may ultimately lead to an error.
        /// </summary>
        Warn,

        /// <summary>
        /// Describes a normal operation or condition.
        /// </summary>
        Info,

        /// <summary>
        /// Describes detailed debug or diagnostic information.
        /// </summary>
        Debug
    }
}
