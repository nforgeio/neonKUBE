//-----------------------------------------------------------------------------
// FILE:	    CodeGeneratorSettings.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.
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
using System.Linq;
using System.Text;

namespace Neon.CodeGen
{
    /// <summary>
    /// Specifies code generator settings.
    /// </summary>
    public class CodeGeneratorSettings
    {
        /// <summary>
        /// Constructs an instance with reasonable settings.
        /// </summary>
        /// <param name="targetGroups">
        /// Specifies the target groups to be included in the
        /// generated output code.
        /// </param>
        public CodeGeneratorSettings(params string[] targetGroups)
        {
            Covenant.Requires<ArgumentNullException>(targetGroups != null);

            foreach (var group in targetGroups)
            {
                if (string.IsNullOrEmpty(group))
                {
                    continue;
                }

                if (!TargetGroups.Contains(group, StringComparer.InvariantCultureIgnoreCase))
                {
                    TargetGroups.Add(group);
                }

                if (TargetGroups.Count == 0)
                {
                    throw new ArgumentException("At least one target group must be specified.");
                }
            }
        }

        /// <summary>
        /// Indicates that service client code should be generated.  This defaults to
        /// <c>true</c> and may be set to <c>false</c> when only the data models
        /// need to be generated.
        /// </summary>
        public bool ServiceClients { get; set; } = true;

        /// <summary>
        /// Enhances data model code generation to prevent property loss
        /// for noSQL scenarios where somebody added a model property before
        /// all referencing applications have regenerated their data models.  
        /// This defaults to <c>true</c>.
        /// </summary>
        public bool RoundTrip { get; set; } = true;

        /// <summary>
        /// <para>
        /// Used to select a specific target groups to be included in the
        /// generated output.
        /// </para>
        /// <note>
        /// All groups will be generated when the <see cref="TargetGroups"/> 
        /// list is empty.
        /// </note>
        /// </summary>
        public List<string> TargetGroups { get; private set; } = new List<string>();

        /// <summary>
        /// Specifies the C# <c>namespace</c> to be used when generating the output
        /// code.  This defaults to <c>Neon.CodeGen.Output</c>.
        /// </summary>
        public string TargetNamespace { get; set; } = "Neon.CodeGen.Output";

        /// <summary>
        /// Specifies the C# <c>namespace</c> to be used to filter the
        /// service and data model classes processed by the code generator.
        /// This is especially handy for unit testing.  This defaults to
        /// <c>null</c> which disables any filtering.
        /// </summary>
        public string SourceNamespace { get; set; }
    }
}
