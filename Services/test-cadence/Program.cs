//-----------------------------------------------------------------------------
// FILE:	    Program.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2020 by neonFORGE LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Service;

namespace CadenceService
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
    ///     <term><b>CADENCE_SERVERS</b>/term>
    ///     <description>
    ///     <i>required</i>: Comma separated HTTP/HTTPS URIs to one or more Cadence cluster servers.
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><b>CADENCE_DOMAIN</b>/term>
    ///     <description>
    ///     <i>required</i>: Specifies the Cadence domain where the workflows will be registered.
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><b>CADENCE_TASKLIST</b>/term>
    ///     <description>
    ///     <i>required</i>: Specifies the Cadence task list for the registered workflows.
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
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public async static Task Main(string[] args)
        {
            await new CadenceTester(NeonServices.TestCadence, serviceMap: NeonServiceMap.Production).RunAsync();
        }
    }
}
