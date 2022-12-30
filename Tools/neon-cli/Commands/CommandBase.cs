//-----------------------------------------------------------------------------
// FILE:	    CommandBase.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright © 2005-2022 by NEONFORGE LLC.  All rights reserved.
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
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Kube;

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
        /// Indicates that command options should be checked against <see cref="ExtendedOptions"/>.
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
        public virtual bool NeedsHostingManager => false;

        /// <inheritdoc/>
        public virtual string SplitItem
        {
            get { return null; }
        }

        /// <inheritdoc/>
        public abstract void Help();

        /// <inheritdoc/>
        public abstract Task RunAsync(CommandLine commandLine);
    }
}
