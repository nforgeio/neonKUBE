//-----------------------------------------------------------------------------
// FILE:	    TestOutputWriter.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2020 by neonFORGE, LLC.  All rights reserved.
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
using System.Threading.Tasks;

using Neon.Common;
using Neon.Cryptography;
using Neon.Diagnostics;
using Neon.IO;
using Neon.Xunit;

using Xunit;
using Xunit.Abstractions;

namespace Neon.Xunit
{
    /// <summary>
    /// <para>
    /// Wraps an <see cref="ITestOutputHelper"/> with a <see cref="TextWriter"/> that can
    /// be used generate output in unit tests that will be included in the captured test log.
    /// </para>
    /// <note>
    /// Only the <c>WriteLine(...)</c> methods are implemented.
    /// </note>
    /// </summary>
    /// <exception cref="NotImplementedException">Thrown for all methods except for  <c>WriteLine()</c>.</exception>
    /// <remarks>
    /// <para>
    /// To use this class, you'll need to obtain a <see cref="ITestOutputHelper"/> instance from Xunit via 
    /// dependency injection by adding a parameter to your test constructor and then creating a
    /// <see cref="TestOutputWriter"/> from it, like:
    /// </para>
    /// <code language="c#">
    /// public class MyTest : IClassFixture&lt;AspNetFixture&gt;
    /// {
    ///     private AspNetFixture               fixture;
    ///     private TestAspNetFixtureClient     client;
    ///     private TestOutputWriter            testWriter;
    ///
    ///     public Test_EndToEnd(AspNetFixture fixture, ITestOutputHelper outputHelper)
    ///     {
    ///         this.fixture    = fixture;
    ///         this.testWriter = new TestOutputWriter(outputHelper);
    ///
    ///         fixture.Start&lt;Startup&gt;(logWriter: testWriter, logLevel: Neon.Diagnostics.LogLevel.Debug);
    ///
    ///         client = new TestAspNetFixtureClient()
    ///         {
    ///             BaseAddress = fixture.BaseAddress
    ///         };
    ///      }
    /// }
    /// </code>
    /// </remarks>
    public class TestOutputWriter : TextWriter
    {
        private ITestOutputHelper outputHelper;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="outputHelper">The test output helper.</param>
        public TestOutputWriter(ITestOutputHelper outputHelper)
        {
            Covenant.Requires<ArgumentNullException>(outputHelper != null, nameof(outputHelper));

            this.outputHelper = outputHelper;
        }

        /// <inheritdoc/>
        public override Encoding Encoding => Encoding.UTF8;

        /// <inheritdoc/>
        public override void Flush()
        {
        }

        /// <inheritdoc/>
        public override async Task FlushAsync()
        {
            await Task.CompletedTask;
        }

        /// <inheritdoc/>
        public override void Write(ulong value)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public override void Write(uint value)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public override void Write(string format, params object[] arg)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public override void Write(string format, object arg0, object arg1, object arg2)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public override void Write(string format, object arg0, object arg1)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public override void Write(string format, object arg0)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public override void Write(string value)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public override void Write(object value)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public override void Write(long value)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public override void Write(int value)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public override void Write(double value)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public override void Write(decimal value)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public override void Write(char[] buffer, int index, int count)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public override void Write(char[] buffer)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public override void Write(char value)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public override void Write(bool value)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public override void Write(float value)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public override Task WriteAsync(string value)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public override Task WriteAsync(char value)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public override Task WriteAsync(char[] buffer, int index, int count)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public override void WriteLine(string format, object arg0)
        {
            outputHelper.WriteLine(format, arg0);
        }

        /// <inheritdoc/>
        public override void WriteLine(ulong value)
        {
            outputHelper.WriteLine(value.ToString());
        }

        /// <inheritdoc/>
        public override void WriteLine(uint value)
        {
            outputHelper.WriteLine(value.ToString());
        }

        /// <inheritdoc/>
        public override void WriteLine(string format, params object[] arg)
        {
            outputHelper.WriteLine(format, arg);
        }

        /// <inheritdoc/>
        public override void WriteLine(string format, object arg0, object arg1, object arg2)
        {
            outputHelper.WriteLine(format, new object[] { arg0, arg1, arg2 });
        }

        /// <inheritdoc/>
        public override void WriteLine(string format, object arg0, object arg1)
        {
            outputHelper.WriteLine(format, new object[] { arg0, arg1 });
        }

        /// <inheritdoc/>
        public override void WriteLine(string value)
        {
            outputHelper.WriteLine(value.ToString());
        }

        /// <inheritdoc/>
        public override void WriteLine(float value)
        {
            outputHelper.WriteLine(value.ToString());
        }

        /// <inheritdoc/>
        public override void WriteLine()
        {
            outputHelper.WriteLine(string.Empty);
        }

        /// <inheritdoc/>
        public override void WriteLine(long value)
        {
            outputHelper.WriteLine(value.ToString());
        }

        /// <inheritdoc/>
        public override void WriteLine(int value)
        {
            outputHelper.WriteLine(value.ToString());
        }

        /// <inheritdoc/>
        public override void WriteLine(double value)
        {
            outputHelper.WriteLine(value.ToString());
        }

        /// <inheritdoc/>
        public override void WriteLine(decimal value)
        {
            outputHelper.WriteLine(value.ToString());
        }

        /// <inheritdoc/>
        public override void WriteLine(char[] buffer, int index, int count)
        {
            outputHelper.WriteLine(new string(buffer, index, count));
        }

        /// <inheritdoc/>
        public override void WriteLine(char[] buffer)
        {
            outputHelper.WriteLine(new string(buffer));
        }

        /// <inheritdoc/>
        public override void WriteLine(char value)
        {
            outputHelper.WriteLine(value.ToString());
        }

        /// <inheritdoc/>
        public override void WriteLine(bool value)
        {
            outputHelper.WriteLine(value.ToString());
        }

        /// <inheritdoc/>
        public override void WriteLine(object value)
        {
            outputHelper.WriteLine(value.ToString());
        }

        /// <inheritdoc/>
        public override async Task WriteLineAsync()
        {
            outputHelper.WriteLine(string.Empty);
            await Task.CompletedTask;
        }

        /// <inheritdoc/>
        public override async Task WriteLineAsync(char value)
        {
            outputHelper.WriteLine(value.ToString());
            await Task.CompletedTask;
        }

        /// <inheritdoc/>
        public override async Task WriteLineAsync(char[] buffer, int index, int count)
        {
            outputHelper.WriteLine(new string(buffer, index, count));
            await Task.CompletedTask;
        }

        /// <inheritdoc/>
        public override async Task WriteLineAsync(string value)
        {
            outputHelper.WriteLine(value.ToString());
            await Task.CompletedTask;
        }
    }
}
