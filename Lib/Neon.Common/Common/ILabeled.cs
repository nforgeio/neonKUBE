//-----------------------------------------------------------------------------
// FILE:	    ILabeled.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2021 by neonFORGE LLC.  All rights reserved.
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
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

using Neon.Common;

namespace Neon.Common
{
    /// <summary>
    /// Used by <see cref="LabelSelector{TItem}"/> to retrieve the label dictionary for
    /// a specific item.  Labels are simply key/value pairs assigned to an object and
    /// <see cref="LabelSelector{TItem}"/> can be used to select items based on their
    /// assigned labels.
    /// </summary>
    public interface ILabeled
    {
        /// <summary>
        /// Returns the label dictionary for the instance.  These are simply key/value
        /// pairs where the key is the label name.  You may return <c>null</c> to indicate
        /// that there are not labels.
        /// </summary>
        /// <returns>The label dictionary or <c>null</c>.</returns>
        /// <remarks>
        /// Label names may be treated as case sensitive or insentive based on how the underlying
        /// dictionary returned was constructed.  Generally though, labels are considered to be
        /// case insensitive so you should probably use <see cref="StringComparison.InvariantCultureIgnoreCase"/>
        /// when constructing your dictionaries.
        /// </remarks>
        IDictionary<string, string> GetLabels();
    }
}
