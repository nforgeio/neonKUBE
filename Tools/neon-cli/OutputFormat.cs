// -----------------------------------------------------------------------------
// FILE:	    OutputFormat.cs
// CONTRIBUTOR: NEONFORGE Team
// COPYRIGHT:   Copyright © 2005-2023 by NEONFORGE LLC.  All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License").
// You may not use this file except in compliance with the License.
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
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NeonCli
{
    /// <summary>
    /// Enumerates the supported command output formats.
    /// </summary>
    public enum OutputFormat
    {
        /// <summary>
        /// No format is specified.
        /// </summary>
        Unspecified = 0,

        /// <summary>
        /// Specifies the JSON format.
        /// </summary>
        Json,

        /// <summary>
        /// Specifies the YAML format.
        /// </summary>
        Yaml
    }
}
