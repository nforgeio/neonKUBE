//-----------------------------------------------------------------------------
// FILE:        AssemblyAttributes.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright © 2005-2024 by NEONFORGE LLC.  All rights reserved.
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
using System.Reflection;

// $note(jefflill):
//
// We need to tag the assembly with a [TargetFrameworkAttribute] that describes
// the target framework.  This is required for debugging and also for running
// unit tests.  Normally, folks have MSBUILD generate these files, but that doesn't
// work for complex solutions due to the scourge of duplicate symbols.
//
// We're relying on [Directory.Build.props] to detect the target framework and
// define the associated build constant.  Note that when no relaveat constant is
// defined, we assume .NET Framework 4.8.
//
// IMPORTANT: [Directory.Build.props] and the code below will need to be updated
//            when we upgrade to a new framework version.
//
// See [Directory.Build.props] for more information.

#if NETSTANDARD2_0
[assembly: global::System.Runtime.Versioning.TargetFrameworkAttribute(".NETStandard,Version=v2.0", FrameworkDisplayName = ".NET Standard 2.0")]
#elif NETSTANDARD2_1
[assembly: global::System.Runtime.Versioning.TargetFrameworkAttribute(".NETStandard,Version=v2.1", FrameworkDisplayName = ".NET Standard 2.1")]
#elif NET6_0
[assembly: global::System.Runtime.Versioning.TargetFrameworkAttribute(".NETCoreApp,Version=v6.0", FrameworkDisplayName = ".NET 6.0")]
#elif NET7_0
[assembly: global::System.Runtime.Versioning.TargetFrameworkAttribute(".NETCoreApp,Version=v7.0", FrameworkDisplayName = ".NET 7.0")]
#elif NET8_0
[assembly: global::System.Runtime.Versioning.TargetFrameworkAttribute(".NETCoreApp,Version=v7.0", FrameworkDisplayName = ".NET 8.0")]
#elif NET48
[assembly: global::System.Runtime.Versioning.TargetFrameworkAttribute(".NETFramework,Version=v4.8", FrameworkDisplayName = ".NET Framework 4.8")]
#else
#error Current framework is not supported.  You'll need to add support above.
#endif
