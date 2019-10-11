//-----------------------------------------------------------------------------
// FILE:	    Program.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Kube;

namespace CadenceTester
{
    /// <summary>
    /// Service entrypoint class.
    /// </summary>
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
            new CadenceTester(NeonServiceMap.Production, NeonServices.TestCadence).RunAsync().Wait();
        }
    }
}
