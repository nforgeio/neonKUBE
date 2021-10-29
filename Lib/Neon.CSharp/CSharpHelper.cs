//-----------------------------------------------------------------------------
// FILE:	    CSharpHelper.cs
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
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Basic.Reference.Assemblies;
using Microsoft.CodeAnalysis;

using Neon.Common;

namespace Neon.CSharp
{
    /// <summary>
    /// C# dynamic compilation related utilities.
    /// </summary>
    public static class CSharpHelper
    {
        /// <summary>
        /// Returns the reference assembles for the current .NET runtime environment.  These are
        /// required when dynamically compiling C# code.
        /// </summary>
        /// <exception cref="NotSupportedException">Thrown when the current runtime environment is not recognized or supported.</exception>
        public static IEnumerable<PortableExecutableReference> RuntimeReferenceAssemblies
        {
            get
            {
                var frameworkVersion = NeonHelper.FrameworkVersion;

                switch (NeonHelper.Framework)
                {
                    case NetFramework.NetFramework:

                        if (frameworkVersion.Major == 4)
                        {
                            if (frameworkVersion.Minor == 6)
                            {
                                return ReferenceAssemblies.Net461;
                            }
                            else if (frameworkVersion.Minor >= 7)
                            {
                                return ReferenceAssemblies.Net472;
                            }
                        }
                        break;

                    case NetFramework.Core:

                        return ReferenceAssemblies.NetCoreApp31;

                    case NetFramework.Net:

                        switch (frameworkVersion.Major)
                        {
                            case 5:

                                return ReferenceAssemblies.Net50;

                            case 6:

                                return ReferenceAssemblies.Net60;
                        }
                        break;
                }

                throw new NotSupportedException($"Framework[{NeonHelper.FrameworkDescription}] is not currently supported.");
            }
        }
    }
}
