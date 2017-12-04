//-----------------------------------------------------------------------------
// FILE:	    AnsibleCommand.cs
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
    /// Implements the <b>ansible</b> commands.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Ansible is not supported on Windows and although it's possible to deploy Ansible
    /// on Mac OSX, we don't want to require it as a dependency to make the experience
    /// the same on Windows and Mac and also to simplify neonCLUSTER setup.  The <b>neoon-cli</b>
    /// implements the <b>neon ansible...</b> commands to map files from the host operating
    /// system into a <b>neoncluster/neon-cli</b> container where Ansible is installed so any
    /// operations can be executed there.
    /// </para>
    /// <para>
    /// These commands are not currently intended to support all Ansible configuration 
    /// scenarios.  For now, 
    /// </para>
    /// </remarks>
    public class AnsibleCommand : CommandBase
    {
        private const string usage = @"
USAGE:

    neon create key
    neon create password [OPTIONS]
";
        /// <inheritdoc/>
        public override string[] Words
        {
            get { return new string[] { "ansible" }; }
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
            Help();
        }

        /// <inheritdoc/>
        public override ShimInfo Shim(DockerShim shim)
        {
            return new ShimInfo(isShimmed: true);
        }
    }
}
