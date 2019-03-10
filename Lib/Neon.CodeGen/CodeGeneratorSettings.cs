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
        public CodeGeneratorSettings()
        {
        }

        /// <summary>
        /// Indicates that service client code should be generated.  This defaults to
        /// <c>true</c> and may be set to <c>false</c> when only the data models
        /// need to be generated.
        /// </summary>
        public bool GenerateServiceClients { get; set; } = true;

        /// <summary>
        /// Enhances data model code generation to prevent property loss
        /// for noSQL scenarios where somebody added a model property before
        /// all referencing applications have regenerated their generated
        /// data models.  This defaults to <c>true</c>.
        /// </summary>
        public bool EnableRoundTrip { get; set; } = true;
        
        /// <summary>
        /// Used to select a specific target group when there are more
        /// than one group defined.
        /// </summary>
        public string TargetGroup { get; set; }

        /// <summary>
        /// The default C# <c>namespace</c> to use when no other namespace is
        /// specified.  This defaults to <c>Neon.CodeGen.Output</c>.
        /// </summary>
        public string DefaultNamespace { get; set; } = "Neon.CodeGen.Output";

        /// <summary>
        /// Used to map case insensitve group names to the C# <c>namespace</c>
        /// to be used when generating service and data models within the
        /// group.  These mappings override <see cref="DefaultNamespace"/>.
        /// </summary>
        public Dictionary<string, string> GroupToNamespace { get; private set; } = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
    }
}
