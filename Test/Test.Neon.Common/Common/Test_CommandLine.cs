//-----------------------------------------------------------------------------
// FILE:	    Test_CommandLine.cs
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
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Xunit;

using Xunit;

// $todo(jefflill):
//
// Test response files and CommandLine.Format()

namespace TestCommon
{
    public class Test_CommandLine
    {
        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void Empty()
        {
            var commandLine = new CommandLine(new string[0]);

            Assert.Empty(commandLine.Items);
            Assert.Empty(commandLine.Arguments);
            Assert.True(!commandLine.HasHelpOption);

            commandLine = new CommandLine();

            Assert.Empty(commandLine.Items);
            Assert.Empty(commandLine.Arguments);
            Assert.True(!commandLine.HasHelpOption);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void Basic()
        {
            var commandLine = new CommandLine(new string[] { "one", "two", "three" });

            Assert.Equal<string>(commandLine.Arguments, new string[] { "one", "two", "three" });
            Assert.True(!commandLine.HasHelpOption);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void Help()
        {
            var commandLine = new CommandLine(new string[] { "one", "--help" });

            Assert.True(commandLine.HasHelpOption);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void DashArgs()
        {
            var commandLine = new CommandLine(new string[] { "foo", "-" });

            Assert.Equal("foo", commandLine.Arguments[0]);
            Assert.Equal("-", commandLine.Arguments[1]);

            commandLine = new CommandLine(new string[] { "foo", "--" });

            Assert.Equal("foo", commandLine.Arguments[0]);
            Assert.Equal("--", commandLine.Arguments[1]);

            commandLine = new CommandLine(new string[] { "foo", "bar", "-" });

            Assert.Equal("foo", commandLine.Arguments[0]);
            Assert.Equal("bar", commandLine.Arguments[1]);
            Assert.Equal("-", commandLine.Arguments[2]);

            commandLine = commandLine.Shift(1);

            Assert.Equal(2, commandLine.Arguments.Length);
            Assert.Equal("bar", commandLine.Arguments[0]);
            Assert.Equal("-", commandLine.Arguments[1]);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void OptionsBasic()
        {
            var commandLine = new CommandLine(new string[] { "one", "-a=1", "two", "-b=2" });

            Assert.Equal<string>(new string[] { "one", "two" }, commandLine.Arguments);
            Assert.Equal("1", commandLine.GetOption("-a"));
            Assert.Equal("2", commandLine.GetOption("-b"));
            Assert.Equal("3", commandLine.GetOption("-c", "3"));
            Assert.Null(commandLine.GetOption("-d"));
            Assert.True(commandLine.HasOption("-a"));
            Assert.False(commandLine.HasOption("-d"));

            Assert.False(commandLine.HasHelpOption);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void OptionsCaseSensitive()
        {
            var commandLine = new CommandLine(new string[] { "one", "-a=1", "-A=2" });

            Assert.Equal("1", commandLine.GetOption("-a"));
            Assert.Equal("2", commandLine.GetOption("-A"));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void OptionsDoubleDash()
        {
            var commandLine = new CommandLine(new string[] { "one", "--a=1", "--A=2" });

            Assert.Equal("1", commandLine.GetOption("--a"));
            Assert.Equal("2", commandLine.GetOption("--A"));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void OptionsValues()
        {
            var commandLine = new CommandLine(new string[] { "one", "-a=1", "-a=2" });

            Assert.Equal(new string[] { "1", "2" }, commandLine.GetOptionValues("-a"));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void OptionDefinition()
        {
            // Test matching the first option definition.

            var commandLine = new CommandLine(new string[] { "one", "-a=1", "-b=2" });

            commandLine.DefineOption("-a", "--advanced");

            Assert.Equal("1", commandLine.GetOption("-a"));
            Assert.Equal("1", commandLine.GetOption("--advanced"));
            Assert.Equal("2", commandLine.GetOption("-b"));
            Assert.True(commandLine.HasOption("-a"));
            Assert.True(commandLine.HasOption("--advanced"));
            Assert.True(commandLine.HasOption("-b"));

            // Test matching the second option definition.

            commandLine = new CommandLine(new string[] { "one", "--advanced=1", "-b=2" });

            commandLine.DefineOption("-a", "--advanced");

            Assert.Equal("1", commandLine.GetOption("-a"));
            Assert.Equal("1", commandLine.GetOption("--advanced"));
            Assert.Equal("2", commandLine.GetOption("-b"));
            Assert.True(commandLine.HasOption("-a"));
            Assert.True(commandLine.HasOption("--advanced"));
            Assert.True(commandLine.HasOption("-b"));

            // Test default values.

            commandLine = new CommandLine(new string[] { "one", "--advanced", "-b=2" });

            commandLine.DefineOption("-a", "--advanced").Default = "TEST";

            Assert.Equal("TEST", commandLine.GetOption("-a"));
            Assert.Equal("TEST", commandLine.GetOption("--advanced"));
            Assert.Equal("OVERRIDE", commandLine.GetOption("-a", "OVERRIDE"));
            Assert.Equal("OVERRIDE", commandLine.GetOption("--advanced", "OVERRIDE"));
            Assert.Equal("2", commandLine.GetOption("-b"));
            Assert.True(commandLine.HasOption("-a"));
            Assert.True(commandLine.HasOption("--advanced"));
            Assert.True(commandLine.HasOption("-b"));

            // Test obtaining multiple values.

            commandLine = new CommandLine(new string[] { "one", "--advanced=1", "-b=2", "-a=3", "-a=4", "--advanced=5", "-b=6" });

            commandLine.DefineOption("-a", "--advanced");

            Assert.Equal(new string[] { "2", "6" }, commandLine.GetOptionValues("-b"));
            Assert.Equal(new string[] { "1", "3", "4", "5" }, commandLine.GetOptionValues("-a"));
            Assert.Equal(new string[] { "1", "3", "4", "5" }, commandLine.GetOptionValues("--advanced"));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void OptionFlags()
        {
            var commandLine = new CommandLine(new string[] { "-a" });

            Assert.True(commandLine.GetFlag("-a"));
            Assert.False(commandLine.GetFlag("-b"));

            commandLine = new CommandLine(new string[] { "-q" });

            commandLine.DefineOption("-q", "--quiet");
            Assert.True(commandLine.GetFlag("-q"));
            Assert.True(commandLine.GetFlag("--quiet"));
            Assert.False(commandLine.GetFlag("-b"));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void GetArguments()
        {
            var commandLine = new CommandLine(new string[] { "one", "two", "three" });

            Assert.Equal(new string[] { "one", "two", "three" }, commandLine.GetArguments());
            Assert.Equal(new string[] { "two", "three" }, commandLine.GetArguments(1));
            Assert.Equal(new string[] { "three" }, commandLine.GetArguments(2));
            Assert.Equal(new string[] { }, commandLine.GetArguments(3));

            commandLine = new CommandLine(new string[] { });

            Assert.Equal(new string[] { }, commandLine.GetArguments(3));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void StartsWithArgs()
        {
            var commandLine = new CommandLine(new string[] { "one", "two", "three" });

            Assert.True(commandLine.StartsWithArgs("one"));
            Assert.True(commandLine.StartsWithArgs("one", "two"));
            Assert.True(commandLine.StartsWithArgs("one", "two", "three"));
            Assert.True(commandLine.StartsWithArgs("one", "two", "three"));

            Assert.False(commandLine.StartsWithArgs());
            Assert.False(commandLine.StartsWithArgs("one", "two", "three", "four"));
            Assert.False(commandLine.StartsWithArgs("x"));
            Assert.False(commandLine.StartsWithArgs("one", "x"));
            Assert.False(commandLine.StartsWithArgs("one", "two", "three", "x"));

            Assert.Throws<ArgumentException>(() => commandLine.StartsWithArgs("one", null));

            commandLine = new CommandLine(new string[] { "--test", "one", "two", "three" });

            Assert.True(commandLine.StartsWithArgs("one"));
            Assert.True(commandLine.StartsWithArgs("one", "two"));
            Assert.True(commandLine.StartsWithArgs("one", "two", "three"));
            Assert.True(commandLine.StartsWithArgs("one", "two", "three"));

            Assert.False(commandLine.StartsWithArgs());
            Assert.False(commandLine.StartsWithArgs("one", "two", "three", "four"));
            Assert.False(commandLine.StartsWithArgs("x"));
            Assert.False(commandLine.StartsWithArgs("one", "x"));
            Assert.False(commandLine.StartsWithArgs("one", "two", "three", "x"));

            Assert.Throws<ArgumentException>(() => commandLine.StartsWithArgs("one", null));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void Split()
        {
            //-------------------------
            var commandLine = new CommandLine(new string[] { "one", "--x", "two", "three"});
            var split = commandLine.Split();

            Assert.Equal(split.Left.Items, new string[] { "one", "--x", "two", "three"}, new CollectionComparer<string>());
            Assert.Null(split.Right);

            //-------------------------
            commandLine = new CommandLine(new string[] { "one", "--x", "two", "three", "--", "four", "five" });
            split = commandLine.Split();

            Assert.Equal(split.Left.Items, new string[] { "one", "--x", "two", "three" }, new CollectionComparer<string>());
            Assert.Equal(split.Right.Items, new string[] { "four", "five" }, new CollectionComparer<string>());

            //-------------------------
            commandLine = new CommandLine(new string[] { "one", "--x", "two", "three", "splitter", "four", "five" });
            split = commandLine.Split("splitter");

            Assert.Equal(split.Left.Items, new string[] { "one", "--x", "two", "three" }, new CollectionComparer<string>());
            Assert.Equal(split.Right.Items, new string[] { "four", "five" }, new CollectionComparer<string>());

            //-------------------------
            commandLine = new CommandLine(new string[] { "one", "--x", "two", "three", "splitter", "four", "five" });
            split = commandLine.Split("splitter", addSplitterToRight: true);

            Assert.Equal(split.Left.Items, new string[] { "one", "--x", "two", "three" }, new CollectionComparer<string>());
            Assert.Equal(split.Right.Items, new string[] { "splitter", "four", "five" }, new CollectionComparer<string>());

            //-------------------------
            commandLine = new CommandLine(new string[] { "--" });
            split = commandLine.Split();

            Assert.Empty(split.Left.Items);
            Assert.Empty(split.Right.Items);

            //-------------------------
            commandLine = new CommandLine(new string[] { "left", "--left0", "--left1=1", "--left2", "2", "--", "right", "--right0", "--right1=1", "--right2", "2" });
            split = commandLine.Split();

            Assert.Equal(split.Left.Items, new string[] { "left", "--left0", "--left1=1", "--left2", "2" }, new CollectionComparer<string>());
            Assert.Equal(split.Right.Items, new string[] { "right", "--right0", "--right1=1", "--right2", "2" }, new CollectionComparer<string>());

            //-------------------------
            commandLine = new CommandLine(new string[] { "left", "--left0", "--left1=1", "--left2", "2", "--", "right", "--right0", "--right1=1", "--right2", "2" });
            split = commandLine.Split(addSplitterToRight: true);

            Assert.Equal(split.Left.Items, new string[] { "left", "--left0", "--left1=1", "--left2", "2" }, new CollectionComparer<string>());
            Assert.Equal(split.Right.Items, new string[] { "--", "right", "--right0", "--right1=1", "--right2", "2" }, new CollectionComparer<string>());

            //-------------------------
            commandLine = new CommandLine(new string[] { "left", "--left0", "--left1=1", "--left2", "2", "XX", "right", "--right0", "--right1=1", "--right2", "2" });
            split = commandLine.Split("XX");

            Assert.Equal(split.Left.Items, new string[] { "left", "--left0", "--left1=1", "--left2", "2" }, new CollectionComparer<string>());
            Assert.Equal(split.Right.Items, new string[] { "right", "--right0", "--right1=1", "--right2", "2" }, new CollectionComparer<string>());

            //-------------------------
            commandLine = new CommandLine(new string[] { "cmd", "left", "--left0", "--left1=1", "--left2", "2", "--", "right", "--right0", "--right1=1", "--right2", "2" });
            commandLine = commandLine.Shift(1);
            split = commandLine.Split("--");

            Assert.Equal(split.Left.Items, new string[] { "left", "--left0", "--left1=1", "--left2", "2" }, new CollectionComparer<string>());
            Assert.Equal(split.Right.Items, new string[] { "right", "--right0", "--right1=1", "--right2", "2" }, new CollectionComparer<string>());
        }
    }
}
