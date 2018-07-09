//-----------------------------------------------------------------------------
// FILE:	    CreateCypherCommand.cs
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
    /// Implements the <b>create cypher</b> command.
    /// </summary>
    public class CreateCypherCommand : CommandBase
    {
        private const string usage = @"
Generates a cryptographically random 16-byte key suitable for encrypting 
Consul and Weave network traffic and writes as Base64 to standard output.

USAGE:

    neon create cypher
";
        /// <inheritdoc/>
        public override string[] Words
        {
            get { return new string[] { "create", "cypher" }; }
        }

        /// <inheritdoc/>
        public override void Help()
        {
            Console.WriteLine(usage);
        }

        /// <inheritdoc/>
        public override void Run(CommandLine commandLine)
        {
            Console.Write(Convert.ToBase64String(NeonHelper.RandBytes(16)));
        }

        /// <inheritdoc/>
        public override DockerShimInfo Shim(DockerShim shim)
        {
            return new DockerShimInfo(shimability: DockerShimability.None);
        }
    }
}
