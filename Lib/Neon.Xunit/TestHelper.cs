//-----------------------------------------------------------------------------
// FILE:	    TestHelper.cs
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
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Diagnostics;
using Neon.IO;
using Neon.Service;
using Neon.Xunit;

using Xunit;

namespace Neon.Xunit
{
    /// <summary>
    /// Misc local unit test helpers.
    /// </summary>
    public static class TestHelper
    {
        /// <summary>
        /// Creates and optionally populates a temporary test folder with test files.
        /// </summary>
        /// <param name="files">
        /// The files to be created.  The first item in each tuple entry will be 
        /// the local file name and the second the contents of the file.
        /// </param>
        /// <returns>The <see cref="TempFolder"/>.</returns>
        /// <remarks>
        /// <note>
        /// Ensure that the <see cref="TempFolder"/> returned is disposed so it and
        /// any files within will be deleted.
        /// </note>
        /// </remarks>
        public static TempFolder CreateTestFolder(params Tuple<string, string>[] files)
        {
            var folder = new TempFolder();

            if (files != null)
            {
                foreach (var file in files)
                {
                    File.WriteAllText(Path.Combine(folder.Path, file.Item1), file.Item2 ?? string.Empty);
                }
            }

            return folder;
        }

        /// <summary>
        /// Creates and populates a temporary test folder with a test file.
        /// </summary>
        /// <param name="data">The file name</param>
        /// <param name="filename">The file data.</param>
        /// <returns>The <see cref="TempFolder"/>.</returns>
        /// <remarks>
        /// <note>
        /// Ensure that the <see cref="TempFolder"/> returned is disposed so it and
        /// any files within will be deleted.
        /// </note>
        /// </remarks>
        public static TempFolder TempFolder(string filename, string data)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(filename), nameof(filename));

            var folder = new TempFolder();

            File.WriteAllText(Path.Combine(folder.Path, filename), data ?? string.Empty);

            return folder;
        }

        /// <summary>
        /// Ensures that two enumerations contain the same items, possibly in different
        /// orders.  This is similar to <see cref="Assert.Equal{T}(IEnumerable{T}, IEnumerable{T})"/>
        /// but it doesn't enforce the item order.  This uses the default equality comparer.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="expected">The expected items.</param>
        /// <param name="collection">The collection being tested.</param>
        /// <exception cref="Exception">Various exceptions are thrown if the collections are not equivalent.</exception>
        public static void AssertEquivalent<T>(IEnumerable<T> expected, IEnumerable<T> collection)
        {
            Covenant.Requires<ArgumentNullException>(expected != null, nameof(expected));
            Covenant.Requires<ArgumentNullException>(collection != null, nameof(collection));

            // $todo(jefflill): 
            //
            // This is a simple but stupid order n^2 algorithm which will blow
            // up for larger lists.

            var expectedCount = expected.Count();
            var collectionCount = collection.Count();

            if (expectedCount != collectionCount)
            {
                throw new AssertException($"Expected [expected.Count()={expectedCount}] does not equal [collection.Count()={collectionCount}].");
            }

            foreach (var item in expected)
            {
                Assert.Contains(item, collection);
            }

            foreach (var item in collection)
            {
                Assert.Contains(item, expected);
            }
        }

        /// <summary>
        /// Ensures that two enumerations do not contain the same items, possibly in different
        /// orders.  This is similar to <see cref="Assert.Equal{T}(IEnumerable{T}, IEnumerable{T})"/>
        /// but it doesn't enforce the item order.  This uses the default equality comparer.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="expected">The expected items.</param>
        /// <param name="collection">The collection being tested.</param>
        /// <exception cref="Exception">Various exceptions are thrown if the collections are not equivalent.</exception>
        public static void AssertNotEquivalent<T>(IEnumerable<T> expected, IEnumerable<T> collection)
        {
            Covenant.Requires<ArgumentNullException>(expected != null, nameof(expected));
            Covenant.Requires<ArgumentNullException>(collection != null, nameof(collection));

            try
            {
                AssertEquivalent<T>(expected, collection);
            }
            catch
            {
                return;
            }

            throw new ArgumentException("Collections are equivalent.");
        }

        /// <summary>
        /// Ensures that two enumerations contain the same items, possibly in different
        /// orders.  This is similar to <see cref="Assert.Equal{T}(IEnumerable{T}, IEnumerable{T})"/>
        /// but it doesn't enforce the item order using an equality comparer.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="expected">The expected items.</param>
        /// <param name="collection">The collection being tested.</param>
        /// <param name="comparer">The comparer used to equate objects in the collection with the expected object</param>
        /// <exception cref="Exception">Various exceptions are thrown if the collections are not equivalent.</exception>
        public static void AssertEquivalent<T>(IEnumerable<T> expected, IEnumerable<T> collection, IEqualityComparer<T> comparer)
        {
            Covenant.Requires<ArgumentNullException>(expected != null, nameof(expected));
            Covenant.Requires<ArgumentNullException>(collection != null, nameof(collection));
            Covenant.Requires<ArgumentNullException>(comparer != null, nameof(comparer));

            // $todo(jefflill): 
            //
            // This is a simple but stupid order n^2 algorithm which will blow
            // up for larger lists.

            var expectedCount = expected.Count();
            var collectionCount = collection.Count();

            if (expectedCount != collectionCount)
            {
                throw new AssertException($"Expected [expected.Count()={expectedCount}] does not equal [collection.Count()={collectionCount}].");
            }

            foreach (var item in expected)
            {
                Assert.Contains(item, collection, comparer);
            }

            foreach (var item in collection)
            {
                Assert.Contains(item, expected, comparer);
            }
        }

        /// <summary>
        /// Ensures that two enumerations do not contain the same items, possibly in different
        /// orders.  This is similar to <see cref="Assert.Equal{T}(IEnumerable{T}, IEnumerable{T})"/>
        /// but it doesn't enforce the item order using an equality comparer.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="expected">The expected items.</param>
        /// <param name="collection">The collection being tested.</param>
        /// <param name="comparer">The comparer used to equate objects in the collection with the expected object</param>
        /// <exception cref="Exception">Various exceptions are thrown if the collections are not equivalent.</exception>
        public static void AssertNotEquivalent<T>(IEnumerable<T> expected, IEnumerable<T> collection, IEqualityComparer<T> comparer)
        {
            Covenant.Requires<ArgumentNullException>(expected != null, nameof(expected));
            Covenant.Requires<ArgumentNullException>(collection != null, nameof(collection));
            Covenant.Requires<ArgumentNullException>(comparer != null, nameof(comparer));

            try
            {
                AssertEquivalent<T>(expected, collection, comparer);
            }
            catch
            {
                return;
            }

            throw new ArgumentException("Collections are equivalent.");
        }

        /// <summary>
        /// Ensures that two dictionaries contain the same items using the default equality comparer.
        /// </summary>
        /// <typeparam name="TKey">Specifies the dictionary key type.</typeparam>
        /// <typeparam name="TValue">Specifies the dictionary value type.</typeparam>
        /// <param name="expected">The expected items.</param>
        /// <param name="dictionary">The collection being tested.</param>
        /// <exception cref="Exception">Various exceptions are thrown if the dictionaries are not equivalent.</exception>
        public static void AssertEquivalent<TKey, TValue>(IDictionary<TKey, TValue> expected, IDictionary<TKey, TValue> dictionary)
        {
            Covenant.Requires<ArgumentNullException>(expected != null, nameof(expected));
            Covenant.Requires<ArgumentNullException>(dictionary != null, nameof(dictionary));

            var expectedCount = expected.Count();
            var dictionaryCount = dictionary.Count();

            if (expectedCount != dictionaryCount)
            {
                throw new AssertException($"Expected [expected.Count()={expectedCount}] does not equal [dictionary.Count()={dictionaryCount}].");
            }

            foreach (var item in expected)
            {
                if (!dictionary.TryGetValue(item.Key, out var found))
                {
                    throw new AssertException($"Item [key={item.Key}] is not in [dictionary].");
                }

                try
                {
                    Assert.Equal(item.Value, found);
                }
                catch
                {
                    throw new AssertException($"Item value for [expected[{item.Key}={item.Value}] != [dictionary[{item.Key}]={found}]");
                }
            }
        }

        /// <summary>
        /// Ensures that two dictionaries do not contain the same items using the default equality comparer.
        /// </summary>
        /// <typeparam name="TKey">Specifies the dictionary key type.</typeparam>
        /// <typeparam name="TValue">Specifies the dictionary value type.</typeparam>
        /// <param name="expected">The expected items.</param>
        /// <param name="dictionary">The collection being tested.</param>
        /// <exception cref="Exception">Various exceptions are thrown if the dictionaries are not equivalent.</exception>
        public static void AssertNotEquivalent<TKey, TValue>(IDictionary<TKey, TValue> expected, IDictionary<TKey, TValue> dictionary)
        {
            Covenant.Requires<ArgumentNullException>(expected != null, nameof(expected));
            Covenant.Requires<ArgumentNullException>(dictionary != null, nameof(dictionary));

            try
            {
                AssertEquivalent<TKey, TValue>(expected, dictionary);
            }
            catch
            {
                return;
            }

            throw new ArgumentException("Dictionaries are equivalent.");
        }

        /// <summary>Ensures that two dictionaries contain the same items using an equality comparer.
        /// </summary>
        /// <typeparam name="TKey">Specifies the dictionary key type.</typeparam>
        /// <typeparam name="TValue">Specifies the dictionary value type.</typeparam>
        /// <param name="expected">The expected items.</param>
        /// <param name="dictionary">The collection being tested.</param>
        /// <param name="comparer">The equality comparer to be used.</param>
        /// <exception cref="Exception">Various exceptions are thrown if the dictionaries are not equivalent.</exception>
        public static void AssertEquivalent<TKey, TValue>(IDictionary<TKey, TValue> expected, IDictionary<TKey, TValue> dictionary, IEqualityComparer<TValue> comparer)
        {
            Covenant.Requires<ArgumentNullException>(expected != null, nameof(expected));
            Covenant.Requires<ArgumentNullException>(dictionary != null, nameof(dictionary));
            Covenant.Requires<ArgumentNullException>(comparer != null, nameof(comparer));

            var expectedCount = expected.Count();
            var dictionaryCount = dictionary.Count();

            if (expectedCount != dictionaryCount)
            {
                throw new AssertException($"Expected [expected.Count()={expectedCount}] does not equal [dictionary.Count()={dictionaryCount}].");
            }

            foreach (var item in expected)
            {
                if (!dictionary.TryGetValue(item.Key, out var found))
                {
                    throw new AssertException($"Item [key={item.Key}] is not in [dictionary].");
                }

                try
                {
                    Assert.Equal(item.Value, found, comparer);
                }
                catch
                {
                    throw new AssertException($"Item value for [expected[{item.Key}={item.Value}] != [dictionary[{item.Key}]={found}]");
                }
            }
        }

        /// <summary>Ensures that two dictionaries do not contain the same items using an equality comparer.
        /// </summary>
        /// <typeparam name="TKey">Specifies the dictionary key type.</typeparam>
        /// <typeparam name="TValue">Specifies the dictionary value type.</typeparam>
        /// <param name="expected">The expected items.</param>
        /// <param name="dictionary">The collection being tested.</param>
        /// <param name="comparer">The equality comparer to be used.</param>
        /// <exception cref="Exception">Various exceptions are thrown if the dictionaries are not equivalent.</exception>
        public static void AssertNotEquivalent<TKey, TValue>(IDictionary<TKey, TValue> expected, IDictionary<TKey, TValue> dictionary, IEqualityComparer<TValue> comparer)
        {
            Covenant.Requires<ArgumentNullException>(expected != null, nameof(expected));
            Covenant.Requires<ArgumentNullException>(dictionary != null, nameof(dictionary));
            Covenant.Requires<ArgumentNullException>(comparer != null, nameof(comparer));

            try
            {
                AssertEquivalent<TKey, TValue>(expected, dictionary, comparer);
            }
            catch
            {
                return;
            }

            throw new ArgumentException("Dictionaries are equivalent.");
        }

        /// <summary>
        /// Compares two strings such that platform line ending differences will be
        /// ignored.  This works by removing any embedded carriage returns before
        /// performing the comparision.
        /// </summary>
        /// <param name="expected">The expected value.</param>
        /// <param name="actual">The actual valut.</param>
        public static void AssertEqualLines(string expected, string actual)
        {
            if (expected != null)
            {
                expected = expected.Replace("\r", string.Empty);
            }

            if (actual != null)
            {
                actual = actual.Replace("\r", string.Empty);
            }

            Assert.Equal(expected, actual);
        }

        /// <summary>
        /// Verifies that an action throws a <typeparamref name="TException"/> or an
        /// <see cref="AggregateException"/> that contains <typeparamref name="TException"/>.
        /// </summary>
        /// <typeparam name="TException">The required exception type.</typeparam>
        /// <param name="action">The test action.</param>
        public static void AssertThrows<TException>(Action action)
            where TException : Exception
        {
            Covenant.Requires<ArgumentNullException>(action != null, nameof(action));

            try
            {
                action();
                Assert.True(false, $"Expected: {nameof(TException)}\r\nActual:   (no exception thrown)");
            }
            catch (Exception e)
            {
                if (e is TException || e.Contains<TException>())
                {
                    return;
                }

                Assert.True(false, $"Expected: {nameof(TException)}\r\nActual:   {e.GetType().Name}");
            }
        }

        /// <summary>
        /// Verifies that an asynchronous action throws a <typeparamref name="TException"/> or an
        /// <see cref="AggregateException"/> that contains <typeparamref name="TException"/>.
        /// </summary>
        /// <typeparam name="TException">The required exception type.</typeparam>
        /// <param name="action">The test action.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task AssertThrowsAsync<TException>(Func<Task> action)
            where TException : Exception
        {
            Covenant.Requires<ArgumentNullException>(action != null, nameof(action));

            try
            {
                await action();
                Assert.True(false, $"Expected: {typeof(TException).FullName}\r\nActual:   (no exception thrown)");
            }
            catch (Exception e)
            {
                if (e is TException || e.Contains<TException>())
                {
                    return;
                }

                Assert.True(false, $"Expected: {typeof(TException).FullName}\r\nActual:   {e.GetType().Name}");
            }
        }

        /// <summary>
        /// Used to run a <see cref="TestFixture"/> outside of a unit test.
        /// </summary>
        /// <typeparam name="T">Specifies the test type.</typeparam>
        /// <param name="args">
        /// Optional parameters that will be passed to the constructor after the
        /// fixture parameter.  Note that the number of parameters and their types
        /// must match the constructor parameters after the fixture one.
        /// </param>
        /// <remarks>
        /// <para>
        /// This is often used to run a <see cref="NeonService"/> using <see cref="NeonServiceFixture{TService}"/>
        /// or a collection of <see cref="NeonService"/> instances for debugging purposes using a combination
        /// of a <see cref="ComposedFixture"/> with <see cref="NeonServiceFixture{TService}"/> sub-fixtures.
        /// But, this can also be used for any <see cref="ITestFixture"/> implementation.
        /// </para>
        /// <para>
        /// You'll need to implement a test class that derives from a <see cref="IClassFixture{TFixture}"/>
        /// implementation and optionally implements <see cref="IDisposable"/>.  You'll pass
        /// your test type as <typeparamref name="T"/>.  Your test class must include a public
        /// constructor that accepts a single parameter with the test fixture type and
        /// a public method with no parameters called <c>public void Run()</c>.
        /// </para>
        /// <para>
        /// This will look something like:
        /// </para>
        /// <code language="C#">
        /// public class MyTestRunner : IClassFixture&lt;ComposedFixture&gt;
        /// {
        ///     private ComposedFixture                     composedFixture;
        ///     private NatsFixture                         natsFixture;
        ///     private NeonServiceFixture&lt;QueueService&gt;    queueServiceFixture;
        /// 
        ///     public MyTestRunner(ComposedFixture fixture)
        ///     {
        ///         this.composedFixture = fixture;
        /// 
        ///         composedFixture.Start(
        ///             () =>
        ///             {
        ///                 composedFixture.AddFixture("nats", new NatsFixture(),
        ///                     natsFixture =>
        ///                     {
        ///                        natsFixture.StartAsComposed();
        ///                     });
        /// 
        ///                 composedFixture.AddServiceFixture("queue-service", new NeonServiceFixture&lt;QueueService&gt;(), () => CreateQueueService());
        ///             });
        /// 
        ///         this.natsFixture         = (NatsFixture)composedFixture["nats"];
        ///         this.queueServiceFixture = (NeonServiceFixture&lt;QueueService&gt;)composedFixture["queue-service"];
        ///     }
        ///     
        ///     public void Run()
        ///     {
        ///         // The runner will stop when this method returns.  You can
        ///         // also use this as an opportunity to perform any initialization.
        ///         // For this example, we're just going to spin slowly forever.
        ///         
        ///         while (true)
        ///         {
        ///             System.Threading.Thread.Sleep(10000);
        ///         }
        ///     }
        /// }
        /// </code>
        /// <para>
        /// This method performs these steps:
        /// </para>
        /// <list type="number">
        ///     <item>
        ///     Perform a runtime check to verify that <typeparamref name="T"/> has a public constructor
        ///     that accepts a single parameter of type <typeparamref name="T"/> as well as any additional
        ///     parameters.
        ///     </item>
        ///     <item>
        ///     Perform a runtime check to ensure that <typeparamref name="T"/> has a <c>public void Run()</c>
        ///     method.
        ///     </item>
        ///     <item>
        ///     Instantiate an instance of the test fixture specified by <see cref="IClassFixture{TFixture}"/>.
        ///     </item>
        ///     <item>
        ///     Instantiate an instance of <typeparamref name="T"/>, passing the test fixture just created
        ///     as the parameter.
        ///     </item>
        ///     <item>
        ///     Call the <c>Run()</c> method and wait for it to return.
        ///     </item>
        ///     <item>
        ///     Dispose the test fixture.
        ///     </item>
        ///     <item>
        ///     Call <see cref="IDisposable.Dispose"/>, if implemented by the test class.
        ///     </item>
        ///     <item>
        ///     The method returns.
        ///     </item>
        /// </list>
        /// </remarks>
        public static void RunFixture<T>(params object[] args)
            where T : class
        {
            // Verify that the test class has the required constructor and Run() method.

            var testType         = typeof(T);
            var classFixtureType = testType.GetInterfaces().SingleOrDefault(type => type.Name.StartsWith("IClassFixture"));

            if (classFixtureType == null || classFixtureType.GenericTypeArguments.Length != 1)
            {
                throw new ArgumentException($"Test type [{testType.Name}] does not derive from an [IClassFixture<ITestFixture>].");
            }

            var fixtureType = classFixtureType.GenericTypeArguments.First();
            var constructor = (ConstructorInfo)null;

            foreach (var constructorInfo in testType.GetConstructors())
            {
                var parameters = constructorInfo.GetParameters();

                if (parameters.Length == 1 + args.Length)
                {
                    if (parameters.First().ParameterType == fixtureType)
                    {
                        constructor = constructorInfo;
                        break;
                    }
                }
            }

            if (constructor == null)
            {
                throw new ArgumentException($"Test type [{testType.Name}] is missing a public constructor accepting a single [{fixtureType.Name}] paramweter.");
            }

            var runMethod = testType.GetMethod("Run", new Type[] { });

            if (runMethod == null)
            {
                throw new ArgumentException($"Test type [{testType.Name}] is missing a [public void Run()] method.");
            }

            // Construct the test fixture.

            var fixture = Activator.CreateInstance(fixtureType);
            T   test    = null;

            try
            {
                // Construct the test class and call it's [Run()] method.

                var parameters = new object[1 + args.Length];

                parameters[0] = fixture;

                for (int i = 0; i < args.Length; i++)
                {
                    parameters[i + 1] = args[i];
                }

                test = (T)constructor.Invoke(parameters);

                runMethod.Invoke(test, null);
            }
            finally
            {
                var fixtureDisposable = fixture as IDisposable;

                if (fixtureDisposable != null)
                {
                    fixtureDisposable.Dispose();
                }

                var testDisposable = test as IDisposable;

                if (testDisposable != null)
                {
                    testDisposable.Dispose();
                }
            }
        }

        // Use by [ResetDocker()] to determine when we've started running tests from
        // a new test class so the method will know when to actually reset Docker state.

        private static Type previousTestClass = null;

        /// <summary>
        /// Resets Docker state by removing all containers, volumes, networks and
        /// optionally the Docker image cache.  This is useful ensuring that Docker
        /// is in a known state.  This also disables <b>swarm mode</b>.
        /// </summary>
        /// <param name="testClass">Specifies the current test class or pass <c>null</c> to force the reset).</param>
        /// <param name="pruneImages">Optionally prunes the Docker image cache.</param>
        /// <remarks>
        /// <para>
        /// This method works by comparing the <paramref name="testClass"/> passed
        /// with any previous test class passed.  The method only resets the Docker
        /// state when the test class changes.  This prevents Docker from being reset
        /// when every test in the same class runs (which will probably break tests).
        /// </para>
        /// <note>
        /// This does not support different test classes that use Docker running in
        /// parallel.
        /// </note>
        /// </remarks>
        public static void ResetDocker(Type testClass, bool pruneImages = false)
        {
            if (testClass != null && testClass == previousTestClass)
            {
                return;
            }

            previousTestClass = testClass;

            // Make sure we're not in Swarm mode

            NeonHelper.ExecuteCapture(NeonHelper.DockerCli, new object[] { "swarm", "leave", "--force" }).EnsureSuccess();

            // Remove all containers

            var result = NeonHelper.ExecuteCapture(NeonHelper.DockerCli, new object[] { "pa", "--all", "--quiet" });

            result.EnsureSuccess();

            using (var reader = new StringReader(result.OutputText))
            {
                foreach (var line in reader.Lines())
                {
                    if (string.IsNullOrEmpty(line))
                    {
                        continue;
                    }

                    NeonHelper.ExecuteCapture(NeonHelper.DockerCli, new object[] { "rm", line, "--force" }).EnsureSuccess();
                }
            }

            // Remove all volumes

            result = NeonHelper.ExecuteCapture(NeonHelper.DockerCli, new object[] { "volume", "ls", "--format", "{{ .Name }}" });

            result.EnsureSuccess();

            using (var reader = new StringReader(result.OutputText))
            {
                foreach (var line in reader.Lines())
                {
                    if (string.IsNullOrEmpty(line))
                    {
                        continue;
                    }

                    NeonHelper.ExecuteCapture(NeonHelper.DockerCli, new object[] { "volume", "rm", line.Trim(), "--force" }).EnsureSuccess();
                }
            }
        }
    }
}
