//-----------------------------------------------------------------------------
// FILE:	    YamlDotNetExtensions.cs
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
using System.Diagnostics.Contracts;
using System.IO;
using System.Text;

using YamlDotNet;

namespace YamlDotNet.RepresentationModel
{
    /// <summary>
    /// YamlDotNet related class extensions.
    /// </summary>
    public static class YamlDotNetExtensions
    {
        /// <summary>
        /// Initializes a <see cref="YamlStream"/> with text.
        /// </summary>
        /// <param name="stream">The YAML stream.</param>
        /// <param name="text">The text to be loaded.</param>
        public static void Load(this YamlStream stream, string text)
        {
            Covenant.Requires<ArgumentNullException>(text != null);

            using (var reader = new StringReader(text))
            {
                stream.Load(reader);
            }
        }
    }
}
