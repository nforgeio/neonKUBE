//-----------------------------------------------------------------------------
// FILE:        CommandBase.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:   Copyright © 2005-2023 by NEONFORGE LLC.  All rights reserved.
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
using System.CommandLine;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OperatorCli.Commands
{
    /// <summary>
    /// Base command line command class.
    /// </summary>
    internal class CommandBase : Command
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="name">Specifies the command name.</param>
        /// <param name="description">Specifies the command description.</param>
        protected CommandBase(string name, string description) : base(name, description)
        {
        }

        /// <summary>
        /// $todo(marcusbooyah): 
        /// 
        /// This seems to be somewhat misnamed.  If this is actually the command handler,
        /// why isn't it virtual so it can be overridden and what's up with the message
        /// parameter?
        /// </summary>
        /// <param name="message"></param>
        /// <returns>The command's exit code.</returns>
        protected int HandleCommand(string message)
        {
            Console.WriteLine(message);
            return 0;
        }
    }
}
