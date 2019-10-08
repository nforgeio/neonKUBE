//-----------------------------------------------------------------------------
// FILE:	    CommandFile.cs
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
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Neon.Common;

namespace Neon.Kube
{
    /// <summary>
    /// Describes a file to be included in a <see cref="CommandBundle"/>.
    /// </summary>
    public class CommandFile
    {
        private string      path;
        private string      text;
        private byte[]      data;

        /// <summary>
        /// Constructor.
        /// </summary>
        public CommandFile()
        {
        }

        /// <summary>
        /// The relative path of the file within the bundle.
        /// </summary>
        public string Path
        {
            get { return path; }

            set
            {
                Covenant.Requires<ArgumentNullException>(!string.IsNullOrWhiteSpace(value), nameof(value));
                Covenant.Requires<ArgumentException>(value[0] != '/', nameof(value), "Only relative file paths are allowed.");

                path = value;
            }
        }

        /// <summary>
        /// The file text.  This will be uploaded encoded as UTF-8.
        /// </summary>
        /// <remarks>
        /// <note>
        /// No transformations will be performed on the text.  Specifically, Windows style line endings
        /// <b>will not</b> be converted to Linux standard TAB characters will not be expanded into
        /// spaces.  You'll need perform these yourself if necessary.
        /// </note>
        /// <note>
        /// Only one of <see cref="Text"/> or <see cref="Data"/> may be specified.
        /// </note>
        /// </remarks>
        public string Text
        {
            get { return text; }

            set
            {
                if (data != null)
                {
                    throw new InvalidOperationException($"Only one of [{nameof(Text)}] or [{nameof(Data)}] may be specified.");
                }

                text = value;
            }
        }

        /// <summary>
        /// The file binary data.
        /// </summary>
        /// <remarks>
        /// <note>
        /// Only one of <see cref="Text"/> or <see cref="Data"/> may be specified.
        /// </note>
        /// </remarks>
        public byte[] Data
        {
            get { return data; }

            set
            {
                if (text != null)
                {
                    throw new InvalidOperationException($"Only one of [{nameof(Text)}] or [{nameof(Data)}] may be specified.");
                }

                data = value;
            }
        }

        /// <summary>
        /// Indicates whether the file should be marked as executable after being
        /// unpacked on the Linux machine.
        /// </summary>
        public bool IsExecutable { get; set; }
    }
}
