//-----------------------------------------------------------------------------
// FILE:	    ControllerAttribute.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:	Copyright © 2005-2023 by NEONFORGE LLC.  All rights reserved.
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

namespace Neon.Kube.Operator.Attributes
{
    /// <summary>
    /// Used to exclude a component from assembly scanning when building the operator.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface)]
    public class ControllerAttribute : Attribute
    {
        /// <summary>
        /// Whether to ignore the controller when scanning assemblies.
        /// </summary>
        public bool Ignore { get; set; } = false;

        /// <summary>
        /// Constructor
        /// </summary>
        public ControllerAttribute(
            bool ignore = false)
        {
            this.Ignore = ignore;
        }
    }
}
