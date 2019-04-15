//-----------------------------------------------------------------------------
// FILE:	    JsonExtensions.cs
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
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

namespace Neon.Data
{
    /// <summary>
    /// Newtonsoft related extension methods.
    /// </summary>
    public static class JsonExtensions
    {
        //---------------------------------------------------------------------
        // JsonSerializerSettings extensions

        /// <summary>
        /// Copies the settings from one <see cref="JsonSerializerSettings"/> instance to another.
        /// </summary>
        /// <param name="source">The source settings.</param>
        /// <param name="target">The target instance.</param>
        public static void CopyTo(this JsonSerializerSettings source, JsonSerializerSettings target)
        {
            target.CheckAdditionalContent         = source.CheckAdditionalContent;
            target.ConstructorHandling            = source.ConstructorHandling;
            target.Context                        = source.Context;
            target.ContractResolver               = source.ContractResolver;
            target.Converters                     = source.Converters;
            target.Culture                        = source.Culture;
            target.DateFormatHandling             = source.DateFormatHandling;
            target.DateFormatString               = source.DateFormatString;
            target.DateParseHandling              = source.DateParseHandling;
            target.DateTimeZoneHandling           = source.DateTimeZoneHandling;
            target.DefaultValueHandling           = source.DefaultValueHandling;
            target.EqualityComparer               = source.EqualityComparer;
            target.Error                          = source.Error;
            target.FloatFormatHandling            = source.FloatFormatHandling;
            target.FloatParseHandling             = source.FloatParseHandling;
            target.Formatting                     = source.Formatting;
            target.MaxDepth                       = source.MaxDepth;
            target.MetadataPropertyHandling       = source.MetadataPropertyHandling;
            target.MissingMemberHandling          = source.MissingMemberHandling;
            target.NullValueHandling              = source.NullValueHandling;
            target.ObjectCreationHandling         = source.ObjectCreationHandling;
            target.PreserveReferencesHandling     = source.PreserveReferencesHandling;
            target.ReferenceLoopHandling          = source.ReferenceLoopHandling;
            target.ReferenceResolverProvider      = source.ReferenceResolverProvider;
            target.SerializationBinder            = source.SerializationBinder;
            target.StringEscapeHandling           = source.StringEscapeHandling;
            target.TraceWriter                    = source.TraceWriter;
            target.TypeNameAssemblyFormatHandling = source.TypeNameAssemblyFormatHandling;
            target.TypeNameHandling               = source.TypeNameHandling;
        }
    }
}
