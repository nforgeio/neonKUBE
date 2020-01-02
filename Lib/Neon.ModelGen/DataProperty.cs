//-----------------------------------------------------------------------------
// FILE:	    DataProperty.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2020 by neonFORGE, LLC.  All rights reserved.
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

using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.Text;

using Newtonsoft.Json.Linq;

using Neon.Common;

namespace Neon.ModelGen
{
    /// <summary>
    /// Describes a <see cref="DataModel"/> property.
    /// </summary>
    internal class DataProperty
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="output">The code generator output.</param>
        public DataProperty(ModelGeneratorOutput output)
        {
            Covenant.Requires<ArgumentNullException>(output != null, nameof(output));

            this.Output = output;
        }

        public ModelGeneratorOutput Output { get; private set; }

        /// <summary>
        /// True when this property is not to be serialized.
        /// </summary>
        public bool Ignore { get; set; }

        /// <summary>
        /// The property type.
        /// </summary>
        public Type Type { get; set; }

        /// <summary>
        /// Returns <c>true</c> if the property type is nullable.
        /// </summary>
        public bool IsNullable => Nullable.GetUnderlyingType(Type) != null;

        /// <summary>
        /// <para>
        /// Returns <c>true</c> if the property is to be included in
        /// the generated <see cref="Object.GetHashCode()"/> method's
        /// hash code computation.
        /// </para>
        /// <note>
        /// At least one property must be tagged with this for 
        /// <see cref="Object.GetHashCode()"/> to work.
        /// </note>
        /// </summary>
        public bool IsHashSource { get; set; }

        /// <summary>
        /// The property name for generated code.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The property name to be used for serialization.
        /// </summary>
        public string SerializedName { get; set; }

        /// <summary>
        /// Controls the order for which this property will be serialized.
        /// </summary>
        public int Order { get; set; }

        /// <summary>
        /// Property presence requirements.
        /// </summary>
        public Required Required { get; set; } = Required.Default;

        /// <summary>
        /// Returns <c>true</c> if the property type requires conversion
        /// to an object before assignment to a <see cref="JObject"/> property.
        /// </summary>
        public bool RequiresObjectification
        {
            get => !(Type.IsPrimitive || Type == typeof(string) || Type == typeof(Decimal));
        }

        /// <summary>
        /// Set to the value specified by a <see cref="DefaultValueAttribute"/> 
        /// on the property or the default value for the property type.
        /// </summary>
        public object DefaultValue { get; set; }

        /// <summary>
        /// Returns <see cref="DefaultValue"/> as an expression string or
        /// <c>null</c> if the property has no default value or if it's
        /// the same as the default value for the property type.
        /// </summary>
        public string DefaultValueLiteral
        {
            get
            {
                if (DefaultValue == null)
                {
                    return null;
                }

                if (Type.IsPrimitive || Type == typeof(string) || Type == typeof(Decimal) || Type.IsEnum)
                {
                    if (Type == typeof(string))
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
                        var defaultObject = Activator.CreateInstance(Type);

                        if (defaultObject.Equals(DefaultValue))
                        {
                            return null;
                        }
                        else
                        {
                            if (Type.IsEnum)
                            {
                                return $"{Type.Name}.{DefaultValue}";
                            }
                            else if (Type == typeof(bool))
                            {
                                return NeonHelper.ToBoolString((bool)DefaultValue);
                            }
                            else
                            {
                                return DefaultValue.ToString();
                            }
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

        /// <summary>
        /// Set the value specified by a <see cref="JsonPropertyAttribute.DefaultValueHandling"/>
        /// attribute on the property.
        /// </summary>
        public DefaultValueHandling DefaultValueHandling { get; set; } = DefaultValueHandling.Include;
    }
}
