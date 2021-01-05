//-----------------------------------------------------------------------------
// FILE:        IStaticFile.cs
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
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Neon.Common;

namespace Neon.IO
{
    /// <summary>
    /// <para>
    /// Describes a logical file in a static file system.  This is used to abstract access
    /// to static files read from an assembly's embedded resources or potentially from other
    /// sources using Linux style paths.
    /// </para>
    /// <para>
    /// This is currently used to emulate a tree of <see cref="IStaticDirectory"/> and 
    /// <see cref="IStaticFile"/> instances loaded from an assembly's embedded resources
    /// via the <see cref="AssemblyExtensions.GetResourceFileSystem(Assembly, string)"/>
    /// extension method.
    /// </para>
    /// <note>
    /// <b>IMPORTANT: </b>Implementations need to be thread-safe.
    /// </note>
    /// <note>
    /// In general, file and directory name lookup case sensitivity should probably be
    /// case insensitive for most purposes but this is an implementation specific detail. 
    /// </note>
    /// </summary>
    /// <threadsafety instance="true"/>
    public interface IStaticFile
    {
        /// <summary>
        /// Returns the file name.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Returns the fully qualified Linux style path for the static file relative to
        /// the static root directory.
        /// </summary>
        public string Path { get; }

        /// <summary>
        /// Reads the file contents as a UTF-8 encoded string.
        /// </summary>
        /// <param name="encoding">Optionally specifies the text encoding.  This defaults to <b>UTF-8</b>,</param>
        /// <returns>The file contents.</returns>
        public string ReadAllText(Encoding encoding = null);

        /// <summary>
        /// Asynchronously reads the file contents as a UTF-8 encoded string.
        /// </summary>
        /// <param name="encoding">Optionally specifies the text encoding.  This defaults to <b>UTF-8</b>,</param>
        /// <returns>The file contents.</returns>
        public Task<string> ReadAllTextAsync(Encoding encoding = null);

        /// <summary>
        /// <para>
        /// Opens a text reader for the file contents.
        /// </para>
        /// <note>
        /// You are responsible disposing the reader returned when you're done with it.
        /// </note>
        /// </summary>
        /// <param name="encoding">Optionally specifies the text encoding.  This defaults to <b>UTF-8</b>,</param>
        /// <returns></returns>
        public TextReader OpenReader(Encoding encoding = null);

        /// <summary>
        /// <para>
        /// Asychronously opens a text reader for the file contents.
        /// </para>
        /// <note>
        /// You are responsible disposing the reader returned when you're done with it.
        /// </note>
        /// </summary>
        /// <param name="encoding">Optionally specifies the text encoding.  This defaults to <b>UTF-8</b>,</param>
        /// <returns></returns>
        public Task<TextReader> OpenReaderAsync(Encoding encoding = null);

        /// <summary>
        /// Reads the file contents as bytes.
        /// </summary>
        /// <returns>The file contents.</returns>
        public byte[] ReadAllBytes();

        /// <summary>
        /// Asynchronously reads the file contents as bytes.
        /// </summary>
        /// <returns>The file contents.</returns>
        public Task<byte[]> ReadAllBytesAsync();

        /// <summary>
        /// <para>
        /// Opens a stream on the file contents.
        /// </para>
        /// <note>
        /// You are responsible disposing the reader returned when you're done with it.
        /// </note>
        /// </summary>
        /// <returns>The <see cref="Stream"/>.</returns>
        public Stream OpenStream();

        /// <summary>
        /// <para>
        /// Asychronously opens a stream on the file contents.
        /// </para>
        /// <note>
        /// You are responsible disposing the reader returned when you're done with it.
        /// </note>
        /// </summary>
        /// <returns>The <see cref="Stream"/>.</returns>
        public Task<Stream> OpenStreamAsync();
    }
}
