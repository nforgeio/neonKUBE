//-----------------------------------------------------------------------------
// FILE:	    Test_PreprocessReader.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2020 by neonFORGE LLC.  All rights reserved.
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
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using Neon.Common;
using Neon.IO;
using Neon.Xunit;

using Xunit;

namespace TestCommon
{
    public class Test_PreprocessReader
    {
        private void SetVariables(PreprocessReader reader, KeyValuePair<string, string>[] variables)
        {
            foreach (var variable in variables)
            {
                reader.Set(variable.Key, variable.Value);
            }
        }

        private Regex       variableRegex              = null;
        private string      defaultVariable            = null;
        private string      defaultEnvironmentVariable = null;
        private int         tabStop                    = 0;
        private bool        expandVariables            = true;
        private bool        stripComments              = true;
        private bool        removeComments             = false;
        private bool        removeBlank                = false;
        private bool        processCommands            = true;
        private char        statementMarker            = '#';
        private int         indent                     = 0;
        private LineEnding  lineEnding                 = LineEnding.Platform;

        private PreprocessReader CreateReader(string input)
        {
            var reader = new PreprocessReader(input)
            {
                DefaultVariable            = defaultVariable,
                DefaultEnvironmentVariable = defaultEnvironmentVariable,
                TabStop                    = tabStop,
                ExpandVariables            = expandVariables,
                StripComments              = stripComments,
                RemoveComments             = removeComments,
                RemoveBlank                = removeBlank,
                ProcessStatements            = processCommands,
                StatementMarker            = statementMarker,
                Indent                     = indent,
                LineEnding                 = lineEnding
            };

            if (variableRegex != null)
            {
                reader.VariableExpansionRegex = variableRegex;
            }

            return reader;
        }

        private async Task VerifyAsync(string input, string output, params KeyValuePair<string, string>[] variables)
        {
            PreprocessReader reader;
            StringBuilder sb;

            reader = CreateReader(input);

            SetVariables(reader, variables);

            Assert.Equal(output, reader.ReadToEnd());

            reader = CreateReader(input);
            SetVariables(reader, variables);

            reader = CreateReader(input);

            reader = CreateReader(input);
            sb = new StringBuilder();

            SetVariables(reader, variables);

            for (var line = reader.ReadLine(); line != null; line = reader.ReadLine())
            {
                sb.AppendLine(line);
            }

            Assert.Equal(output, sb.ToString());

            reader = CreateReader(input);
            sb = new StringBuilder();

            SetVariables(reader, variables);

            for (var line = await reader.ReadLineAsync(); line != null; line = await reader.ReadLineAsync())
            {
                sb.AppendLine(line);
            }

            Assert.Equal(output, sb.ToString());

            reader = CreateReader(input);
            sb = new StringBuilder();

            SetVariables(reader, variables);

            foreach (var line in reader.Lines())
            {
                sb.AppendLine(line);
            }

            Assert.Equal(output, sb.ToString());
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void Defaults()
        {
            var reader = new PreprocessReader(new StreamReader(new MemoryStream()));

            Assert.Equal(PreprocessReader.AngleVariableExpansionRegex, reader.VariableExpansionRegex);
            Assert.True(reader.ProcessStatements);
            Assert.True(reader.StripComments);
            Assert.False(reader.RemoveComments);
            Assert.False(reader.RemoveBlank);
            Assert.True(reader.StatementMarker == '#');
            Assert.Equal(0, reader.TabStop);
            Assert.Equal(LineEnding.Platform, reader.LineEnding);
            Assert.True(reader.ExpandVariables);
            Assert.Equal(0, reader.Indent);
            Assert.Null(reader.DefaultVariable);
            Assert.Null(reader.DefaultEnvironmentVariable);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public async Task Empty()
        {
            await VerifyAsync(string.Empty, string.Empty);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public async Task NoChange()
        {
            const string input =
@"This is a test

of
the
emergency broadcasting system.
";
            await VerifyAsync(input, input);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public async Task Comments()
        {
            await VerifyAsync(
@"// This is a comment
     // This is a comment
This is a test
// This is a comment
of the emergency // not a comment
broadcasting system
//

a
ab
abc

",
@"

This is a test

of the emergency // not a comment
broadcasting system


a
ab
abc

");
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public async Task VariablesDefault()
        {
            await VerifyAsync(
@"
$<hello>
---$<hello>---
$<hello> $<bye>
$<ref> $<bye>
",
@"
Hello World!
---Hello World!---
Hello World! Goodbye!
Hello World! Goodbye!
",
                new KeyValuePair<string, string>[]
                {
                    new KeyValuePair<string, string>("hello", "Hello World!"),
                    new KeyValuePair<string, string>("bye", "Goodbye!"),
                    new KeyValuePair<string, string>("ref", "$<hello>")
                });

            // Verify that environment variables work.

            Environment.SetEnvironmentVariable("NF_TEST_VARIABLE", "Hello World!");

            await VerifyAsync(
@"
---$<<NF_TEST_VARIABLE>>---
",
@"
---Hello World!---
");
            // Verify that a recursive variable definition is detected.

            await Assert.ThrowsAsync<FormatException>(
                async () =>
                {
                    await VerifyAsync("$<recursive>", string.Empty,
                        new KeyValuePair<string, string>[]
                        {
                            new KeyValuePair<string, string>("recursive", "$<test>"),
                            new KeyValuePair<string, string>("test", "$<recursive>")
                        });
                });
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public async Task VariablesCurly()
        {
            try
            {
                variableRegex = PreprocessReader.CurlyVariableExpansionRegex;

                await VerifyAsync(
@"
${hello}
>>>${hello}<<<
${hello} ${bye}
${ref} ${bye}
",
@"
Hello World!
>>>Hello World!<<<
Hello World! Goodbye!
Hello World! Goodbye!
",
                    new KeyValuePair<string, string>[]
                    {
                        new KeyValuePair<string, string>("hello", "Hello World!"),
                        new KeyValuePair<string, string>("bye", "Goodbye!"),
                        new KeyValuePair<string, string>("ref", "${hello}")
                    });

                // Verify that environment variables work.

                Environment.SetEnvironmentVariable("NF_TEST_VARIABLE", "Hello World!");

                await VerifyAsync(
@"
>>>${{NF_TEST_VARIABLE}}<<<
",
@"
>>>Hello World!<<<
");
                // Verify that a recursive variable definition is detected.

                await Assert.ThrowsAsync<FormatException>(
                    async () =>
                    {
                        await VerifyAsync("${recursive}", string.Empty,
                            new KeyValuePair<string, string>[]
                            {
                                new KeyValuePair<string, string>("recursive", "${test}"),
                                new KeyValuePair<string, string>("test", "${recursive}")
                            });
                    });
            }
            finally
            {
                variableRegex = null;
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public async Task CheckForUndefinedVariables()
        {
            // Verify that we can disable undefined variable checks.

            try
            {
                defaultVariable = string.Empty;

                await VerifyAsync(
@"
---$<hello>---
",
@"
------
");
                defaultVariable = "DEFAULT";

                await VerifyAsync(
@"
---$<hello>---
",
@"
---DEFAULT---
");
            }
            finally
            {
                defaultVariable = null;
            }

            // Verify checks for simple undefined variable references.

            await Assert.ThrowsAsync<KeyNotFoundException>(async () => await VerifyAsync(">>>$<hello><<<", string.Empty));

            // Verify checks for indirect undefined variable references.

            await Assert.ThrowsAsync<KeyNotFoundException>(
                async () =>
                {
                    await VerifyAsync(">>>$<ref><<<", string.Empty,
                    new KeyValuePair<string, string>[]
                        {
                            new KeyValuePair<string, string>("ref", "$<undefined>")
                        });
                });


            // Verify that undefined environment variables throw a [KeyNotFoundException].

            await Assert.ThrowsAsync<KeyNotFoundException>(
                async () =>
                {
                    await VerifyAsync(
@"
---$<<NF_TEST_VARIABLE>>---
---$<<NF_UNDEFINED_VARIABLE>>---
",
@"
---Hello World!---
---DEFAULT-VALUE---
");
                });

            // Verify that undefined environment variables are replaced with
            // a default value.

            Environment.SetEnvironmentVariable("NF_TEST_VARIABLE", "Hello World!");

            defaultEnvironmentVariable = "DEFAULT-VALUE";

            try
            {
                await VerifyAsync(
@"
---$<<NF_TEST_VARIABLE>>---
---$<<NF_UNDEFINED_VARIABLE>>---
",
@"
---Hello World!---
---DEFAULT-VALUE---
");
            }
            finally
            {
                defaultEnvironmentVariable = null;
            }
            // Verify that undefined environment variables are replaced with
            // a default value.

            Environment.SetEnvironmentVariable("NF_TEST_VARIABLE", "Hello World!");

            defaultEnvironmentVariable = "DEFAULT-VALUE";

            try
            {
                await VerifyAsync(
@"
---$<<NF_TEST_VARIABLE>>---
---$<<NF_UNDEFINED_VARIABLE>>---
",
@"
---Hello World!---
---DEFAULT-VALUE---
");
            }
            finally
            {
                defaultEnvironmentVariable = null;
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public async Task VariablesParen()
        {
            try
            {
                variableRegex = PreprocessReader.ParenVariableExpansionRegex;

                await VerifyAsync(
@"
$(hello)
>>>$(hello)<<<
$(hello) $(bye)
$(ref) $(bye)
",
@"
Hello World!
>>>Hello World!<<<
Hello World! Goodbye!
Hello World! Goodbye!
",
                new KeyValuePair<string, string>[]
                {
                    new KeyValuePair<string, string>("hello", "Hello World!"),
                    new KeyValuePair<string, string>("bye", "Goodbye!"),
                    new KeyValuePair<string, string>("ref", "$(hello)")
                });

                // Verify that environment variables work.

                Environment.SetEnvironmentVariable("NF_TEST_VARIABLE", "Hello World!");

                await VerifyAsync(
@"
>>>$((NF_TEST_VARIABLE))<<<
",
@"
>>>Hello World!<<<
");
                // Verify that a recursive variable definition is detected.

                await Assert.ThrowsAsync<FormatException>(
                    async () =>
                    {
                        await VerifyAsync("$(recursive)", string.Empty,
                            new KeyValuePair<string, string>[]
                            {
                            new KeyValuePair<string, string>("recursive", "$(test)"),
                            new KeyValuePair<string, string>("test", "$(recursive)")
                            });
                    });
            }
            finally
            {
                variableRegex = null;
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public async Task Define()
        {
            await VerifyAsync(
@"
#define test = 1
$<test>
    #define test=2
    $<test>
#define test =3
$<test>
#define test= 4
$<test>
#define test
>>>$<test><<<
#define foo=$<bar>
#define bar=FOOBAR
$<foo>
",
@"

1

    2

3

4

>>><<<


FOOBAR
");
            // Verify that we detect invalid #define statements.

            await Assert.ThrowsAsync<FormatException>(async () => await VerifyAsync("#define", string.Empty));
            await Assert.ThrowsAsync<FormatException>(async () => await VerifyAsync("#define =", string.Empty));
            await Assert.ThrowsAsync<FormatException>(async () => await VerifyAsync("#define %%2 = 10", string.Empty));
            await Assert.ThrowsAsync<FormatException>(async () => await VerifyAsync("#define test junk", string.Empty));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public async Task If()
        {
            await VerifyAsync(
@"
#if test==test
one
#endif
",
@"

one

");
            await VerifyAsync(
@"
#if test!=test
one
#endif
",
@"



");
            await VerifyAsync(
@"
#if test == test 
one
#else
two
#endif
",
@"

one



");
            await VerifyAsync(
@"
#if test == xxx 
one
#else
two
#endif
",
@"



two

");
            await VerifyAsync(
@"
#if test != test 
one
#else
two
#endif
",
@"



two

");
            await VerifyAsync(
@"
#if test != xxx 
one
#else
two
#endif
",
@"

one



");
            await VerifyAsync(
@"
#if defined(test) 
one
#else
two
#endif
",
@"



two

");
            await VerifyAsync(
@"
#if undefined(test) 
one
#else
two
#endif
",
@"

one



");
            await VerifyAsync(
@"
#define test
#if defined(test) 
one
#else
two
#endif
",
@"


one



");
            await VerifyAsync(
@"
#define test
#if undefined(test) 
one
#else
two
#endif
",
@"




two

");
            await VerifyAsync(
@"
#define test=1
#if defined(test) 
one
#else
two
#endif
",
@"


one



");
            await VerifyAsync(
@"
#define test=1
#if undefined(test) 
one
#else
two
#endif
",
@"




two

");
            await VerifyAsync(
@"
#define a=1
#define b=2
#if $<a>==$<b>
one
#else
two
#endif
",
@"





two

");
            await VerifyAsync(
@"
#define a=1
#define b=2
#if $<a>!=$<b> 
one
#else
two
#endif
",
@"



one



");
            await VerifyAsync(
@"
#define test=1
#if $<test>==1 
    #if $<test>==1
    inner-then
    #endif
#else
    #if $<test>==1
    inner-else
    #endif
#endif
",
@"



    inner-then






");
            await VerifyAsync(
@"
#define test=1
#if undefined(test) 
    #if $<test>==1
    inner-then
    #endif
#else
    #if $<test>==1
    inner-else
    #endif
#endif
",
@"







    inner-else


");
            // Verify that we detect invalid #if statements.

            await Assert.ThrowsAsync<FormatException>(async () => await VerifyAsync("#if\r\n#endif", string.Empty));
            await Assert.ThrowsAsync<FormatException>(async () => await VerifyAsync("#if =\r\n#endif", string.Empty));
            await Assert.ThrowsAsync<FormatException>(async () => await VerifyAsync("#if <>\r\n#endif", string.Empty));
            await Assert.ThrowsAsync<FormatException>(async () => await VerifyAsync("#if defined\r\n#endif", string.Empty));
            await Assert.ThrowsAsync<FormatException>(async () => await VerifyAsync("#if defined()\r\n#endif", string.Empty));
            await Assert.ThrowsAsync<FormatException>(async () => await VerifyAsync("#if defined()\r\n#endif", string.Empty));
            await Assert.ThrowsAsync<FormatException>(async () => await VerifyAsync("#if", string.Empty));
            await Assert.ThrowsAsync<FormatException>(async () => await VerifyAsync("#else", string.Empty));
            await Assert.ThrowsAsync<FormatException>(async () => await VerifyAsync("#endif", string.Empty));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public async Task Switch()
        {
            await VerifyAsync(
@"
#switch one
A
#case zero
B
#case one
C
#case two
D
#default
E
#endswitch
",
@"





C





");
            await VerifyAsync(
@"
#define test=one
#switch $<test>
A
#case zero
B
#case one
C
#case two
D
#default
E
#endswitch
",
@"






C





");
            await VerifyAsync(
@"
#define test=two
#switch $<test>
A
#case zero
B
#case one
C
#case two
D
#default
E
#endswitch
",
@"








D



");
            await VerifyAsync(
@"
#define test=100
#switch $<test>
A
#case zero
B
#case one
C
#case two
D
#default
E
#endswitch
",
@"










E

");
            // Verify that we detect invalid [#switch] statements.

            await Assert.ThrowsAsync<FormatException>(async () => await VerifyAsync("#switch 10\r\n#case 10\r\n#case 10\r\n#endswitch", string.Empty));
            await Assert.ThrowsAsync<FormatException>(async () => await VerifyAsync("#switch 10\r\n#default\r\n#case 10\r\n#endswitch", string.Empty));
            await Assert.ThrowsAsync<FormatException>(async () => await VerifyAsync("#switch", string.Empty));
            await Assert.ThrowsAsync<FormatException>(async () => await VerifyAsync("#case", string.Empty));
            await Assert.ThrowsAsync<FormatException>(async () => await VerifyAsync("#default", string.Empty));
            await Assert.ThrowsAsync<FormatException>(async () => await VerifyAsync("#endswitch", string.Empty));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public async Task NotImplemented()
        {
            var reader = new PreprocessReader(string.Empty);

            Assert.Throws<NotImplementedException>(() => reader.Peek());
            Assert.Throws<NotImplementedException>(() => reader.Read());
            Assert.Throws<NotImplementedException>(() => reader.Read(new char[100], 0, 100));
            await Assert.ThrowsAsync<NotImplementedException>(async () => await reader.ReadAsync(new char[100], 0, 100));
            Assert.Throws<NotImplementedException>(() => reader.ReadBlock(new char[100], 0, 100));
            await Assert.ThrowsAsync<NotImplementedException>(async () => await reader.ReadBlockAsync(new char[100], 0, 100));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public async Task TabStops()
        {
            try
            {
                tabStop = 4;

                await VerifyAsync(
@"
line1
\tline2
-\tline3
--\tline4
---\tline5
----\tline6
\t\tline7
".Replace(@"\t", "\t"),
@"
line1
    line2
-   line3
--  line4
--- line5
----    line6
        line7
");
                tabStop = 0;

                await VerifyAsync(
@"
line1
\tline2
-\tline3
--\tline4
---\tline5
----\tline6
\t\tline7
".Replace(@"\t", "\t"),
@"
line1
\tline2
-\tline3
--\tline4
---\tline5
----\tline6
\t\tline7
".Replace(@"\t", "\t"));
            }
            finally
            {
                tabStop = 0;
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public async Task DisableStripComments()
        {
            try
            {
                stripComments = false;

                await VerifyAsync(
@"
// Hello World!
",
@"
// Hello World!
");
            }
            finally
            {
                stripComments = true;
            }
        }


        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public async Task RemoveComments()
        {
            try
            {
                removeComments = true;

                await VerifyAsync(
@"
// Hello World!
",
@"
");
            }
            finally
            {
                removeComments = false;
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public async Task RemoveBlank()
        {
            try
            {
                removeBlank = true;

                await VerifyAsync(
@"

    
//
// Test
",
@"");
            }
            finally
            {
                removeBlank = false;
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public async Task DisableCommands()
        {
            try
            {
                processCommands = false;

                await VerifyAsync(
@"
#define DEBUG = true
#if defined(DEBUG)
#endif
",
@"
#define DEBUG = true
#if defined(DEBUG)
#endif
");
            }
            finally
            {
                processCommands = true;
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public async Task StatementMarker()
        {
            try
            {
                statementMarker = '@';

                await VerifyAsync(
@"
@define test = true
@if defined(test)
Hello World!
@else
Goodbye World!
@endif
",
@"


Hello World!



");
            }
            finally
            {
                statementMarker = '#';
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public async Task Indent()
        {
            try
            {
                indent = 4;

                await VerifyAsync(
@"
Test
", 
@"
    Test
");
            }
            finally
            {
                indent = 0;
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public async Task LineEndings()
        {
            const string input =
@"line1
line2
line3
";
            using (var reader = new PreprocessReader(input) { LineEnding = LineEnding.CRLF })
            {
                Assert.Equal("line1\r\nline2\r\nline3\r\n", reader.ReadToEnd());
            }

            using (var reader = new PreprocessReader(input) { LineEnding = LineEnding.LF })
            {
                Assert.Equal("line1\nline2\nline3\n", reader.ReadToEnd());
            }

            using (var reader = new PreprocessReader(input) { LineEnding = LineEnding.CRLF })
            {
                Assert.Equal("line1\r\nline2\r\nline3\r\n", await reader.ReadToEndAsync());
            }

            using (var reader = new PreprocessReader(input) { LineEnding = LineEnding.LF })
            {
                Assert.Equal("line1\nline2\nline3\n", await reader.ReadToEndAsync());
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void DisableStatements()
        {
            const string input =
@"# line1
line2
# line3
";
            using (var reader = new PreprocessReader(input) { LineEnding = LineEnding.CRLF })
            {
                reader.ProcessStatements = false;

                Assert.Equal("# line1\r\nline2\r\n# line3\r\n", reader.ReadToEnd());
            }
        }
    }
}
