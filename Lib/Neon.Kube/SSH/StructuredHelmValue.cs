// -----------------------------------------------------------------------------
// FILE:	    StructuredHelmValue.cs
// CONTRIBUTOR: NEONFORGE Team
// COPYRIGHT:   Copyright © 2005-2024 by NEONFORGE LLC.  All rights reserved.
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
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Neon.Kube.SSH
{
    /// <summary>
    /// <para>
    /// Holds a Helm map or array value string.  These values will be
    /// be persisted to a temporary Helm <b>Values.yaml</b> file that
    /// that will be passed to the Helm CLI when installing the chart.
    /// </para>
    /// <note>
    /// The value must not be empty or include any line endings.
    /// </note>
    /// </summary>
    public struct StructuredHelmValue
    {
        //---------------------------------------------------------------------
        // Static members

        private static readonly char[] CRLFChars = new char[] { '\r', '\n' };

        /// <summary>
        /// Implicitly casts a string into a <see cref="StructuredHelmValue"/>.
        /// </summary>
        /// <param name="value">
        /// Specifies a YAML map like <c>{...}</c> or an array like <c>[...]</c>.
        /// The string may not include line endings.
        /// </param>
        public static implicit operator StructuredHelmValue(string value)
        {
            return new StructuredHelmValue(value);
        }

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="value">
        /// Specifies a YAML map like <c>{...}</c> or an array like <c>[...]</c>.
        /// The string may not include line endings.
        /// </param>
        public StructuredHelmValue(string value)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(value), nameof(value));

            var firstCh = value.First();
            var lastCh  = value.Last();
            var error   = (string)null;

            if (firstCh == '{')
            {
                if (lastCh != '}')
                {
                    error = "Invalid map: expected terminating '}'.";
                }
            }
            else if (firstCh == '[')
            {
                if (lastCh != ']')
                {
                    error = "Invalid array: expected terminating ']'.";
                }
            }
            else
            {
                error = "Invalid value: only YAML maps or arrays are allowed.";
            }

            if (error != null)
            {
                throw new ArgumentException(error, paramName: nameof(value));
            }

            if (value.IndexOfAny(CRLFChars) >= 0)
            {
                throw new ArgumentException("Value cannot include line endings.l", paramName: nameof(value));
            }

            Value = value;
        }

        /// <summary>
        /// Returns the value string.
        /// </summary>
        public string Value { get; private set; }
    }
}
