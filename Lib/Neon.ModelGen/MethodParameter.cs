//-----------------------------------------------------------------------------
// FILE:	    MethodParameter.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.
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
using System.Reflection;
using System.Text;

using Neon.Common;

namespace Neon.ModelGen
{
    /// <summary>
    /// Holds information about a service model method parameter.
    /// </summary>
    internal class MethodParameter
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="parameterInfo">The .NET parameter information.</param>
        public MethodParameter(ParameterInfo parameterInfo)
        {
            Covenant.Requires<ArgumentNullException>(parameterInfo != null, nameof(parameterInfo));

            this.ParameterInfo = parameterInfo;
        }

        /// <summary>
        /// Returns the low-level .NET parameter information.
        /// </summary>
        public ParameterInfo ParameterInfo { get; private set; }

        /// <summary>
        /// Returns the parameter name.
        /// </summary>
        public string Name => ParameterInfo.Name;

        /// <summary>
        /// Specifies how the parameter should be passed to the service endpoint.
        /// </summary>
        public Pass Pass { get; set; } = Pass.AsQuery;

        /// <summary>
        /// The parameter or HTTP header name to use when passing the parameter as <see cref="Pass.AsQuery"/>
        /// <see cref="Pass.AsRoute"/>, or <see cref="Pass.AsHeader"/>.  This is ignored for <see cref="Pass.AsBody"/>.
        /// </summary>
        public string SerializedName { get; set; }

        /// <summary>
        /// Returns <c>true</c> if the parameter is optional.
        /// </summary>
        public bool IsOptional { get; set; }

        /// <summary>
        /// Returns the the default for parameters where <see cref="IsOptional"/> is <c>true</c>.
        /// </summary>
        public object DefaultValue { get; set; }

        /// <summary>
        /// Returns <see cref="DefaultValue"/> as an expression string or
        /// <c>null</c> if the parameter is not optional.
        /// </summary>
        public string DefaultValueLiteral
        {
            get
            {
                if (!IsOptional)
                {
                    return null;
                }

                var type = ParameterInfo.ParameterType;

                if (type.IsPrimitive || type == typeof(string) || type == typeof(Decimal) || type.IsEnum || type.FullName.StartsWith("System.Nullable`"))
                {
                    if (type == typeof(string))
                    {
                        if (DefaultValue == null)
                        {
                            return "null";
                        }
                        else
                        {
                            return $"\"{DefaultValue}\"";
                        }
                    }
                    else
                    {
                        if (type.IsEnum)
                        {
                            return $"{type.Name}.{DefaultValue}";
                        }
                        else if (type == typeof(bool))
                        {
                            return NeonHelper.ToBoolString((bool)DefaultValue);
                        }
                        else if (DefaultValue == null)
                        {
                            return "null";
                        }
                        else
                        {
                            return DefaultValue.ToString();
                        }
                    }
                }
                else
                {
                    // Other types can't have default values.

                    return null;
                }
            }
        }
    }
}
