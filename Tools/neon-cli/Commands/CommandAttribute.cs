//-----------------------------------------------------------------------------
// FILE:	    CommandAttribute.cs
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

namespace NeonCli
{
    /// <summary>
    /// Used to tag an <see cref="ICommand"/> for automatic inclusion in a program.
    /// </summary>
    public class CommandAttribute : Attribute
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="disabled">
        /// Optionally disables the command, preventing it fromm being
        /// recognized by the program.
        /// </param>
        public CommandAttribute(bool disabled = false)
        {
            this.Disabled = disabled;
        }

        /// <summary>
        /// Indicates when the command is disabled.
        /// </summary>
        public bool Disabled { get; private set; }
    }
}
