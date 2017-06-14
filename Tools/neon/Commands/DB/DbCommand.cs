//-----------------------------------------------------------------------------
// FILE:	    DbCommand.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2017 by NeonForge, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft;
using Newtonsoft.Json;

using Neon.Cluster;
using Neon.Common;

namespace NeonTool
{
    /// <summary>
    /// Implements the <b>db</b> commands.
    /// </summary>
    public class DbCommand : CommandBase
    {
        private const string usage = @"
Manages common persisted services as pets, not cattle.

USAGE:

    neon db create TYPE [OPTIONS] NAME
    neon db inspect NAME
    neon db list
    neon db ls
    neon db remove NAME
    neon db rm NAME

ARGS:

    NAME    - Service name
    TYPE    - Identifies the desired persisted service type

SERVICE TYPES:

    Couchbase
";
        /// <inheritdoc/>
        public override string[] Words
        {
            get { return new string[] { "db" }; }
        }

        /// <inheritdoc/>
        public override void Help()
        {
            Console.WriteLine(usage);
        }

        /// <inheritdoc/>
        public override void Run(CommandLine commandLine)
        {
            Console.WriteLine(usage);

            if (commandLine.HasHelpOption)
            {
                Program.Exit(0);
            }
            else
            {
                Program.Exit(1);
            }
        }

        /// <inheritdoc/>
        public override ShimInfo Shim(DockerShim shim)
        {
            return new ShimInfo(isShimmed: true, ensureConnection: false);
        }
    }
}
