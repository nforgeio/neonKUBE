//-----------------------------------------------------------------------------
// FILE:	    Program.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.
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
using System.Threading.Tasks;

using Neon.Common;
using Neon.Kube;
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
        /// Returns the program's service implementation.
        /// </summary>
        public static Service Service { get; private set; }

        /// <summary>
        /// The program entry point.
        /// </summary>
        /// <param name="args">The command line arguments.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task Main(string[] args)
        {
            NeonService.Initialize();

            try
            {
                Service = new Service("test-cadence");

                Environment.Exit(await Service.RunAsync());
            }
            catch (Exception e)
            {
                // We really shouldn't see exceptions here but let's log something
                // just in case.  Note that logging may not be initialized yet so
                // we'll just output a string.

                Console.Error.WriteLine(NeonHelper.ExceptionError(e));
                Environment.Exit(-1);
            }
        }
    }
}
