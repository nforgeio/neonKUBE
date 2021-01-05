//-----------------------------------------------------------------------------
// FILE:        StaticResourceFile.cs
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
using System.Diagnostics.Contracts;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Neon.Common;

namespace Neon.IO
{
    /// <summary>
    /// Implements the <see cref="IStaticFile"/> abstractionreferencing an embedded <see cref="Assembly"/> resource.
    /// </summary>
    internal class StaticResourceFile : StaticFileBase
    {
        private Assembly    assembly;
        private string      resourceName;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="assembly">The source assembly.</param>
        /// <param name="resourceName">The name of the resource in the assembly manifest.</param>
        /// <param name="path">The logical path to this file.</param>
        internal StaticResourceFile(Assembly assembly, string resourceName, string path)
            : base(path)
        {
            Covenant.Requires<ArgumentNullException>(assembly != null);
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(resourceName));

            this.assembly     = assembly;
            this.resourceName = resourceName;
        }

        /// <inheritdoc/>
        public override TextReader OpenReader(Encoding encoding = null)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public override Task<TextReader> OpenReaderAsync(Encoding encoding = null)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public override Stream OpenStream()
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public override Task<Stream> OpenStreamAsync()
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public override byte[] ReadAllBytes()
        {
            using (var stream = assembly.GetManifestResourceStream(resourceName))
            {
                return stream.ReadToEnd();
            }
        }

        /// <inheritdoc/>
        public async override Task<byte[]> ReadAllBytesAsync()
        {
            using (var stream = assembly.GetManifestResourceStream(resourceName))
            {
                return await stream.ReadToEndAsync();
            }
        }

        /// <inheritdoc/>
        public override string ReadAllText(Encoding encoding = null)
        {
            encoding = encoding ?? Encoding.UTF8;

            using (var stream = assembly.GetManifestResourceStream(resourceName))
            {
                using (var reader = new StreamReader(stream, encoding))
                {
                    return reader.ReadToEnd();
                }
            }
        }

        /// <inheritdoc/>
        public async override Task<string> ReadAllTextAsync(Encoding encoding = null)
        {
            encoding = encoding ?? Encoding.UTF8;

            using (var stream = assembly.GetManifestResourceStream(resourceName))
            {
                using (var reader = new StreamReader(stream, encoding))
                {
                    return await reader.ReadToEndAsync();
                }
            }
        }
    }
}
