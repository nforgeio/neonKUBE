//-----------------------------------------------------------------------------
// FILE:	    TestHelper.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Diagnostics;
using Neon.IO;
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
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(filename));

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
            Covenant.Requires<ArgumentNullException>(expected != null);
            Covenant.Requires<ArgumentNullException>(collection != null);

            // $todo(jeff.lill): 
            //
            // This is a simple but stupid order n^2 algorithm which will blow
            // up for larger lists.

            var expectedCount   = expected.Count();
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
            Covenant.Requires<ArgumentNullException>(expected != null);
            Covenant.Requires<ArgumentNullException>(collection != null);

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
            Covenant.Requires<ArgumentNullException>(expected != null);
            Covenant.Requires<ArgumentNullException>(collection != null);
            Covenant.Requires<ArgumentNullException>(comparer != null);

            // $todo(jeff.lill): 
            //
            // This is a simple but stupid order n^2 algorithm which will blow
            // up for larger lists.

            var expectedCount   = expected.Count();
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
            Covenant.Requires<ArgumentNullException>(expected != null);
            Covenant.Requires<ArgumentNullException>(collection != null);
            Covenant.Requires<ArgumentNullException>(comparer != null);

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
            Covenant.Requires<ArgumentNullException>(expected != null);
            Covenant.Requires<ArgumentNullException>(dictionary != null);

            var expectedCount   = expected.Count();
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
            Covenant.Requires<ArgumentNullException>(expected != null);
            Covenant.Requires<ArgumentNullException>(dictionary != null);

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
            Covenant.Requires<ArgumentNullException>(expected != null);
            Covenant.Requires<ArgumentNullException>(dictionary != null);
            Covenant.Requires<ArgumentNullException>(comparer != null);

            var expectedCount   = expected.Count();
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
            Covenant.Requires<ArgumentNullException>(expected != null);
            Covenant.Requires<ArgumentNullException>(dictionary != null);
            Covenant.Requires<ArgumentNullException>(comparer != null);

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
    }
}
