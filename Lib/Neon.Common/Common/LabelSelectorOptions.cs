//-----------------------------------------------------------------------------
// FILE:	    LabelSelectorOptions.cs
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
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

using Neon.Common;

namespace Neon.Common
{
    /// <summary>
    /// <see cref="LabelSelector{TItem}"/> related options.
    /// </summary>
    [Flags]
    public enum LabelSelectorOptions
    {
        /// <summary>
        /// No options are selected.
        /// </summary>
        None = 0x0000,

        /// <summary>
        /// <para>
        /// Normally <see cref="LabelSelector{TItem}"/> matches label values using
        /// case sensitive comparisons.  Use this to make the comparisons case
        /// insensitive.
        /// </para>
        /// <note>
        /// Label name case sensitivity is determined by the the dictionaries returned
        /// by the item <see cref="ILabeled.GetLabels()"/> method.
        /// </note>
        /// </summary>
        CaseInsensitiveValues = 0x0001,

        /// <summary>
        /// <see cref="LabelSelector{TItem}"/> defaults to parsing label names
        /// and values to ensure that they are Kubernetes compliant.  Use this 
        /// to disable this so you can use arbitrary labels.
        /// </summary>
        UnConstraintedLabels = 0x0002
    }
}
