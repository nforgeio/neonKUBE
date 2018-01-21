//-----------------------------------------------------------------------------
// FILE:	    IServicesContainer.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

using Neon.Diagnostics;

namespace Neon.Common
{
    /// <summary>
    /// <para>
    /// This interface combines the capabilities of <see cref="IServiceCollection"/> and
    /// <see cref="IServiceProvider"/> to define an object that can dynamically add
    /// and remove service implementations.  See the default implementation 
    /// <see cref="ServiceCollection"/> for more information.
    /// </para>
    /// <note>
    /// Implementations must be thread-safe.
    /// </note>
    /// </summary>
    /// <threadsafety instance="true"/>
    public interface IServiceContainer : IServiceCollection, IServiceProvider
    {
    }
}
