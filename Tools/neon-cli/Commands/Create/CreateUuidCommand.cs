//-----------------------------------------------------------------------------
// FILE:	    CreateUuidCommand.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft;
using Newtonsoft.Json;

using Neon.Common;
using Neon.Hive;

namespace NeonCli
{
    /// <summary>
    /// Implements the <b>create uuid</b> command.
    /// </summary>
    public class CreateUuidCommand : CommandBase
    {
        private const string usage = @"
Generates a globally unique ID like:

    f7bd80fe-a154-4d2c-a730-8ea988a92e67

USAGE:

    neon create uuid
";
        /// <inheritdoc/>
        public override string[] Words
        {
            get { return new string[] { "create", "uuid" }; }
        }

        /// <inheritdoc/>
        public override void Help()
        {
            Console.WriteLine(usage);
        }

        /// <inheritdoc/>
        public override void Run(CommandLine commandLine)
        {
            Console.Write(Guid.NewGuid().ToString("D").ToLowerInvariant());
        }

        /// <inheritdoc/>
        public override DockerShimInfo Shim(DockerShim shim)
        {
            return new DockerShimInfo(shimability: DockerShimability.None);
        }
    }
}
