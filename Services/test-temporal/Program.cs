//-----------------------------------------------------------------------------
// FILE:	    Program.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2020 by neonFORGE LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Service;

namespace TemporalService
{
    /// <summary>
    /// The program entrypoint.
    /// </summary>
    /// <param name="args">The command line arguments.</param>
    /// <remarks>
    /// <para>
    /// This service ignores the command line but recognizes these environment variables.
    /// </para>
    /// <list type="table">
    /// <item>
    ///     <term><b>TEMPORAL_ENDPOINT</b>/term>
    ///     <description>
    ///     <i>required</i>: The Temporal cluster endpoint.
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><b>TEMPORAL_NAMESPACE</b>/term>
    ///     <description>
    ///     <i>required</i>: Specifies the Temporal namespace where the workflows will be registered.
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><b>TEMPORAL_TASKQUEUE</b>/term>
    ///     <description>
    ///     <i>required</i>: Specifies the Temporal task queue for the registered workflows.
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><b>LOG_LEVEL</b>/term>
    ///     <description>
    ///     <i>optional</i>: logging level: CRITICAL, SERROR, ERROR, WARN, INFO, SINFO, DEBUG, or NONE (defaults to INFO).
    ///     </description>
    /// </item>
    /// </list>
    /// </remarks>
    public static class Program
    {
        /// <summary>
        /// The program entry point.
        /// </summary>
        /// <param name="args">The command line arguments.</param>
        /// <remarks>
        /// <para>
        /// This program registers 
        /// </para>
        /// </remarks>
        public static void Main(string[] args)
        {
            new TemporalTester(NeonServiceMap.Production, NeonServices.TestTemporal).RunAsync().Wait();
        }
    }
}
