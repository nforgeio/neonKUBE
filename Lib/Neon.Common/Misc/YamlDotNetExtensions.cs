//-----------------------------------------------------------------------------
// FILE:	    YamlDotNetExtensions.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Diagnostics.Contracts;
using System.IO;
using System.Text;

using YamlDotNet;

namespace YamlDotNet.RepresentationModel
{
    /// <summary>
    /// YamlDotNet related class extensions.
    /// </summary>
    public static class YamlDotNetExtensions
    {
        /// <summary>
        /// Initializes a <see cref="YamlStream"/> with text.
        /// </summary>
        /// <param name="stream">The YAML stream.</param>
        /// <param name="text">The text to be loaded.</param>
        public static void Load(this YamlStream stream, string text)
        {
            Covenant.Requires<ArgumentNullException>(text != null);

            using (var reader = new StringReader(text))
            {
                stream.Load(reader);
            }
        }
    }
}
