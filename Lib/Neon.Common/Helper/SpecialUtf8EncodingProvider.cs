//-----------------------------------------------------------------------------
// FILE:	    SpecialUtf8EncodingProvider.cs
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
    /// This is a special class used by <see cref="NeonHelper.RegisterMisspelledUtf8Provider"/>
    /// to implement UTF-8 encodings with the name <b>utf8</b> misspelled (no dash).
    /// </summary>
    internal class SpecialUtf8EncodingProvider : EncodingProvider
    {
        public override Encoding GetEncoding(int codepage)
        {
            return null;
        }

        public override Encoding GetEncoding(string name)
        {
            if (name.Equals("utf8", StringComparison.InvariantCultureIgnoreCase))
            {
                return Encoding.UTF8;
            }
            else
            {
                return null;
            }
        }
    }
}
