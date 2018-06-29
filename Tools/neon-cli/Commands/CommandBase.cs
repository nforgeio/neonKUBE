//-----------------------------------------------------------------------------
// FILE:	    CommandBase.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Hive;

namespace NeonCli
{
    /// <summary>
    /// An abstract class that has default implementations for selected 
    /// <see cref="ICommand"/> members.
    /// </summary>
    public abstract class CommandBase : ICommand
    {
        /// <inheritdoc/>
        public abstract string[] Words { get; }

        /// <inheritdoc/>
        public virtual string[] AltWords
        {
            get { return null; }
        }

        /// <inheritdoc/>
        public virtual string[] ExtendedOptions
        {
            get { return new string[0]; }
        }

        /// <summary>
        /// Indicates that unknown command options should be checked against <see cref="ExtendedOptions"/>.
        /// This defaults to <c>true</c>.
        /// </summary>
        public virtual bool CheckOptions
        {
            get { return true; }
        }

        /// <inheritdoc/>
        public virtual bool NeedsSshCredentials(CommandLine commandLine)
        {
            return false;
        }

        /// <inheritdoc/>
        public virtual string SplitItem
        {
            get { return null; }
        }

        /// <inheritdoc/>
        public abstract void Help();

        /// <inheritdoc/>
        public abstract void Run(CommandLine commandLine);

        /// <inheritdoc/>
        public abstract DockerShimInfo Shim(DockerShim shim);
    }
}
