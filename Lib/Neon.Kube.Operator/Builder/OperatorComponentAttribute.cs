//-----------------------------------------------------------------------------
// FILE:	    OperatorComponentAttribute.cs
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

namespace Neon.Kube.Operator.Builder
{
    /// <summary>
    /// Used to tag a type implementing an operatgor component.
    /// </summary>
    [AttributeUsage(AttributeTargets.Interface)]
    internal class OperatorComponentAttribute : Attribute
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="componentType">Specifies the component type.</param>
        public OperatorComponentAttribute(OperatorComponentType componentType)
        {
            ComponentType = componentType;
        }

        /// <summary>
        /// Returns the component type.
        /// </summary>
        public OperatorComponentType ComponentType { get; }
    }
}
