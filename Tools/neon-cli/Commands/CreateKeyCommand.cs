//-----------------------------------------------------------------------------
// FILE:	    CreateKeyCommand.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2017 by neonFORGE, LLC.  All rights reserved.

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

using Neon.Cluster;
using Neon.Common;

namespace NeonCli
{
    /// <summary>
    /// Implements the <b>create key</b> command.
    /// </summary>
    public class CreateKeyCommand : CommandBase
    {
        private const string usage = @"
Generates a cryptographically random 16-byte key suitable for encrypting 
Consul and Weave network traffic and writes it encoded as Base64 to the
standard output.

USAGE:

    neon create key
";
        /// <inheritdoc/>
        public override string[] Words
        {
            get { return new string[] { "create", "key" }; }
        }

        /// <inheritdoc/>
        /// <inheritdoc/>
        public override void Help()
        {
            Console.WriteLine(usage);
        }

        /// <inheritdoc/>
        public override void Run(CommandLine commandLine)
        {
            Console.WriteLine(Convert.ToBase64String(NeonHelper.RandBytes(16)));
        }

        /// <inheritdoc/>
        public override ShimInfo Shim(DockerShim shim)
        {
            return new ShimInfo(isShimmed: true);
        }
    }
}
