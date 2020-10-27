//-----------------------------------------------------------------------------
// FILE:	    ModelGeneratorOutput.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2020 by neonFORGE LLC.  All rights reserved.
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
using System.Diagnostics.Contracts;
using System.Text;

namespace Neon.ModelGen
{
    /// <summary>
    /// Holds the output of a model code generation.
    /// </summary>
    public class ModelGeneratorOutput
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        public ModelGeneratorOutput()
        {
        }

        /// <summary>
        /// Indicates whether the coder generator reported any errors.
        /// </summary>
        public bool HasErrors => Errors.Count > 0;

        /// <summary>
        /// Returns the list of code generator errors (if any).
        /// </summary>
        public List<string> Errors { get; private set; } = new List<string>();

        /// <summary>
        /// Appends an error message to the output.
        /// </summary>
        /// <param name="message">The message.</param>
        internal void Error(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                return;
            }

            Errors.Add($"ERROR: {message}");
        }

        /// <summary>
        /// Returns the generated source code.
        /// </summary>
        public string SourceCode { get; internal set; }
    }
}
