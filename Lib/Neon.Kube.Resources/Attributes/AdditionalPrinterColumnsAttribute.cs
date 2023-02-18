//-----------------------------------------------------------------------------
// FILE:	    AdditionalPrinterColumnsAttribute.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:   Copyright © 2005-2023 by NEONFORGE LLC.  All rights reserved.
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
using System.Drawing;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using k8s.Models;

using NJsonSchema;

namespace Neon.Kube.Resources
{
    /// <summary>
    /// The kubectl tool relies on server-side output formatting. Your cluster's API server decides which columns 
    /// are shown by the kubectl get command. You can customize these columns for a CustomResourceDefinition.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
    public sealed class AdditionalPrinterColumnsAttribute : Attribute
    {
        /// <summary>
        /// The name of the column.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// <para>
        /// A column's type field can be any of the following (compare OpenAPI v3 data types):
        /// <list type="bullet">
        /// <item>`integer` – non-floating-point numbers</item>
        /// <item>`number` – floating point numbers</item>
        /// <item>`string` – strings</item>
        /// <item>`boolean` – true or false</item>
        /// <item>`date` – rendered differentially as time since this timestamp.</item>
        /// </list>
        /// If the value inside a CustomResource does not match the type specified for the column, the value is omitted.Use CustomResource validation to ensure that the value types are correct.
        /// </para>
        /// </summary>
        public JsonObjectType Type { get; set; }

        /// <summary>
        /// The description of the column.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// The Json Path of the column.
        /// </summary>
        public string JsonPath { get; set; }

        /// <summary>
        /// <para>
        /// A column's format field can be any of the following:
        /// <list type="bullet">
        /// <item>`integer`</item>
        /// <item>`int64`</item>
        /// <item>`float`</item>
        /// <item>`double`</item>
        /// <item>`byte`</item>
        /// <item>`date`</item>
        /// <item>`date-time`</item>
        /// <item>`password`</item>
        /// </list>
        /// The column's format controls the style used when kubectl prints the value.
        /// </para>
        /// </summary>
        public string Format { get; set; }


        /// <summary>
        /// <para>
        /// Each column includes a priority field. Currently, the priority differentiates between columns shown in 
        /// standard view or wide view (using the -o wide flag).
        /// </para>
        /// <list type="bullet">
        /// <item>Columns with priority 0 are shown in standard view.</item>
        /// <item>Columns with priority greater than 0 are shown only in wide view.</item>
        /// </list>
        /// </summary>
        public int Priority { get; set; }

        /// <summary>
        /// Constructor.
        /// </summary>
        public AdditionalPrinterColumnsAttribute()
        {
        }

        /// <summary>
        /// Converts the attribute to a <see cref="V1CustomResourceColumnDefinition"/>
        /// </summary>
        /// <returns></returns>
        public V1CustomResourceColumnDefinition ToV1CustomResourceColumnDefinition()
        {
            return new V1CustomResourceColumnDefinition()
            {
                Name = Name,
                Description= Description,
                Format= Format,
                Priority = Priority,
                JsonPath= JsonPath,
                Type = Type.ToString(),
            };
        }
    }
}