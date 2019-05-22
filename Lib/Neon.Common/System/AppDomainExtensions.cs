//-----------------------------------------------------------------------------
// FILE:	    AppDomainExtensions.cs
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
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace System
{
    /// <summary>
    /// Implements <see cref="AppDomain"/> extension methods.
    /// </summary>
    public static class AppDomainExtensions
    {
        /// <summary>
        /// Enumerates all non <b>System</b> and <b>Microsoft</b> assemblies currently
        /// loaded in the <see cref="AppDomain"/>.  This can be used as a performance
        /// optimization when you only need to scan user assemblies.
        /// </summary>
        /// <param name="appDomain"></param>
        /// <returns>The enumerated assemblies.</returns>
        /// <remarks>
        /// We also use this to work around this Visual Studio bug: 
        /// <a href="https://github.com/nforgeio/neonKUBE/issues/531"/>
        /// </remarks>
        public static IEnumerable<Assembly> GetUserAssemblies(this AppDomain appDomain)
        {
            return appDomain.GetAssemblies()
                .Where(assembly =>
                {
                    var fullName = assembly.FullName;

                    if (fullName == "System" || fullName == "Microsoft")
                    {
                        return false;
                    }
                    else
                    {
                        return !(fullName.StartsWith("System.") || fullName.StartsWith("Microsoft."));
                    }
                });
        }
    }
}
