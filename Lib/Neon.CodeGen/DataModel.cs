//-----------------------------------------------------------------------------
// FILE:	    DataAttributes.cs
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
    /// Holds information about a data model extracted from a source assembly.
    /// </summary>
    internal class DataModel
    {
        private string typeID;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="sourceType">The source data type.</param>
        /// <param name="codeGenerator">The code generator instance.</param>
        public DataModel(Type sourceType, CodeGenerator codeGenerator)
        {
            Covenant.Requires<ArgumentNullException>(sourceType != null);

            this.SourceType = sourceType;
        }

        /// <summary>
        /// Returns the source type.
        /// </summary>
        public Type SourceType { get; private set; }

        /// <summary>
        /// Returns the targets for the type.
        /// </summary>
        public HashSet<string> Targets { get; private set; } = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);

        /// <summary>
        /// Optional type identifier to be used for persisting the type.
        /// </summary>
        public string TypeID
        {
            get => typeID;

            set
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    typeID = null;
                }
                else
                {
                    typeID = value.Trim();
                }
            }
        }

        /// <summary>
        /// Indicates that the type is an <c>enum</c>.
        /// </summary>
        public bool IsEnum { get; set; }

        /// <summary>
        /// Indicates that the <b>enum</b> type has a <c>[Flags]</c> attribute.
        /// </summary>
        public bool HasEnumFlags { get; set; }

        /// <summary>
        /// Returns the base type name for both <c>enum</c> and other data model types
        /// or <c>null</c> if there is no base type (other than <c>object</c>).
        /// </summary>
        public string BaseTypeName { get; set; }

        /// <summary>
        /// The base <see cref="DataModel"/> or <c>null</c> if the current
        /// data model isn't derived from another.
        /// </summary>
        public DataModel BaseModel { get; set;}

        /// <summary>
        /// Indicates whether the current data model is derived from another model.
        /// </summary>
        public bool IsDerived => BaseTypeName != null;

        /// <summary>
        /// Lists the members for <c>enum</c> types.
        /// </summary>
        public List<EnumMember> EnumMembers { get; private set; } = new List<EnumMember>();

        /// <summary>
        /// Lists the properties for a data model.
        /// </summary>
        public List<DataProperty> Properties { get; private set; } = new List<DataProperty>();

        /// <summary>
        /// Returns the data model properties that satisfy a filter.
        /// </summary>
        /// <param name="selector">The property selector.</param>
        /// <param name="includeInherited">Optionally include properties inherited from ancestor data models.</param>
        /// <returns>The list of selected properties.</returns>
        public IEnumerable<DataProperty> SelectProperties(Func<DataProperty, bool> selector, bool includeInherited = false)
        {
            Covenant.Requires<ArgumentNullException>(selector != null);

            if (!includeInherited)
            {
                return this.Properties.Where(selector);
            }
            else
            {
                var list      = new List<DataProperty>();
                var dataModel = this;

                while (dataModel != null)
                {
                    foreach (var property in dataModel.Properties.Where(selector))
                    {
                        list.Add(property);
                    }

                    if (dataModel.BaseTypeName == null)
                    {
                        break;
                    }

                    dataModel = dataModel.BaseModel;
                }

                return list;
            }
        }
    }
}
