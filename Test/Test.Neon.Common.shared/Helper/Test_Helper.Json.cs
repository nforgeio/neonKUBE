//-----------------------------------------------------------------------------
// FILE:	    Test_Helper.Json.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2017 by NeonForge, LLC.  All rights reserved.


using Neon.Common;

using Xunit;

namespace TestCommon
{
    public partial class Test_Helper
    {
        public class TestClass
        {
            public string Name { get; set; }
            public int Age { get; set; }
        }

        [Fact]
        public void JsonSerialize()
        {
            var before = 
                new TestClass()
                {
                    Name = "Jeff",
                    Age  = 56
                };

            var json = NeonHelper.JsonSerialize(before);

            Assert.StartsWith("{", json);

            var after = NeonHelper.JsonDeserialize<TestClass>(json);

            Assert.Equal("Jeff", after.Name);
            Assert.Equal(56, after.Age);
        }

        [Fact]
        public void JsonClone()
        {
            var value = 
                new TestClass()
                {
                    Name = "Jeff",
                    Age  = 56
                };

            var clone = NeonHelper.JsonClone<TestClass>(value);

            Assert.NotSame(value, clone);
            Assert.Equal(value.Name, clone.Name);
            Assert.Equal(value.Age, clone.Age);
        }
    }
}
