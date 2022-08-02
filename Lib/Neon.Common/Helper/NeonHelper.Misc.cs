//-----------------------------------------------------------------------------
// FILE:	    NeonHelper.Misc.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.
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

using Neon.Data;
using Neon.Tasks;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Neon.Common
{
    public static partial class NeonHelper
    {
        //---------------------------------------------------------------------
        // This is used by [ParseEnumUsingAttributes()] to cache reflected
        // [EnumMember] attributes decorating enumeration values.

        private class EnumMemberSerializationInfo
        {
            /// <summary>
            /// Maps serialized enum [EnumMember] strings to their ordinal values.
            /// </summary>
            public Dictionary<string, long> EnumToStrings = new Dictionary<string, long>(StringComparer.InvariantCultureIgnoreCase);

            /// <summary>
            /// Maps enum ordinal values to their [EnumMember] string.
            /// </summary>
            public Dictionary<long, string> EnumToOrdinals = new Dictionary<long, string>();
        }

        private static readonly Dictionary<Type, EnumMemberSerializationInfo> typeToEnumMemberInfo = new Dictionary<Type, EnumMemberSerializationInfo>();

        //---------------------------------------------------------------------
        // Implementation

        /// <summary>
        /// The environment variable used for unit testing that indicates
        /// that <c>Neon.Service.NeonService</c> should run in test mode and 
        /// locate user test files in the folder specified by this variable
        /// (when set).
        /// </summary>
        public const string TestModeFolderVar = "NF_TESTMODE_FOLDER";

        /// <summary>
        /// Determines whether an integer is odd.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns><c>true</c> if the value is odd.</returns>
        public static bool IsOdd(int value)
        {
            return (value & 1) != 0;
        }

        /// <summary>
        /// Determines whether two nullable values are equal.
        /// </summary>
        /// <typeparam name="T">The base value type.</typeparam>
        /// <param name="v1">Value #1.</param>
        /// <param name="v2">Value #2.</param>
        /// <returns><c>true</c> if the values are equal.</returns>
        public static bool NullableEquals<T>(T? v1, T? v2)
            where T : struct
        {
            if (!v1.HasValue && !v2.HasValue)
            {
                return true;
            }
            else if (v1.HasValue && !v2.HasValue)
            {
                return false;
            }
            else if (!v1.HasValue && v2.HasValue)
            {
                return false;
            }

            return v1.Value.Equals(v2.Value);
        }

        /// <summary>
        /// Converts Windows line endings (CR-LF) to Linux/Unix line endings (LF).
        /// </summary>
        /// <param name="input">The input string or <c>null</c>.</param>
        /// <returns>The input string with converted line endings.</returns>
        public static string ToLinuxLineEndings(string input)
        {
            if (input == null)
            {
                return input;
            }

            return input.Replace("\r\n", "\n");
        }

        /// <summary>
        /// Returns a string representation of an exception suitable for logging.
        /// </summary>
        /// <param name="e">The exception.</param>
        /// <param name="stackTrace">Optionally include the stack track.</param>
        /// <param name="excludeInner">Optionally exclude information about any inner exception.</param>
        /// <param name="depth"><b>INTERNAL USE ONLY:</b> Used to prevent infinite recursion when inner exceptions cycle.</param>
        /// <returns>The error string.</returns>
        public static string ExceptionError(Exception e, bool stackTrace = false, bool excludeInner = false, int depth = 0)
        {
            Covenant.Requires<ArgumentNullException>(e != null, nameof(e));

            var aggregate = e as AggregateException;

            // Loopie encountered a strange situation where the inner exceptions apparently 
            // form a cycle which can result in a stack overflow if we keep drilling down.
            //
            //      https://github.com/nforgeio/neonKUBE/issues/737
            //
            // We're going to handle this by limiting the recursion depth to 8.

            if (aggregate != null && depth < 4)
            {
                if (aggregate.InnerException != null)
                {
                    return ExceptionError(aggregate.InnerException, stackTrace, excludeInner, depth++);
                }
                else if (aggregate.InnerExceptions.Count > 0)
                {
                    return ExceptionError(aggregate.InnerExceptions[0], stackTrace, excludeInner, depth++);
                }
            }

            string message;

            if (e == null)
            {
                message = "NULL Exception";
            }
            else
            {
                message = $"[{e.GetType().FullName}]: {e.Message}";

                if (!excludeInner && e.InnerException != null)
                {
                    message += $" [inner:{e.InnerException.GetType().FullName}: {e.InnerException.Message}]";
                }

                if (stackTrace)
                {
                    message += $" [stack:{new StackTrace(e, skipFrames: 0, fNeedFileInfo: true)}]";
                }
            }

            return message;
        }

        /// <summary>
        /// Starts a new <see cref="Thread"/> to perform an action.
        /// </summary>
        /// <param name="action">The action to be performed.</param>
        /// <param name="maxStackSize">
        /// <para>
        /// Optionally specifies the maximum stack size, in bytes, to be used by the thread, or 
        /// 0 to use the default maximum stack size specified in the header for the executable.
        /// Important for partially trusted code, <paramref name="maxStackSize"/> is ignored if 
        /// it is greater than the default stack size.  No exception is thrown in theis case.
        /// </para>
        /// <para>
        /// This <b>defaults to 0</b> which generally means the stack size will be limited
        /// to <b>1 MiB for 32-bit</b> applications or <b>4 MiB for 64-bit</b> applications.
        /// </para>
        /// </param>
        /// <returns>The <see cref="Thread"/>.</returns>
        public static Thread StartThread(Action action, int maxStackSize = 0)
        {
            Covenant.Requires<ArgumentNullException>(action != null, nameof(action));
            Covenant.Requires<ArgumentException>(maxStackSize >= 0, nameof(maxStackSize));

            var thread = new Thread(new ThreadStart(action), maxStackSize: maxStackSize);

            thread.Start();

            return thread;
        }

        /// <summary>
        /// Starts a new <see cref="Thread"/> to perform a parameterized action with
        /// an object parameter.
        /// </summary>
        /// <param name="action">The action to be performed.</param>
        /// <param name="parameter">The parameter to be passed to the thread action.</param>
        /// <param name="maxStackSize">
        /// <para>
        /// Optionally specifies the maximum stack size, in bytes, to be used by the thread, or 
        /// 0 to use the default maximum stack size specified in the header for the executable.
        /// Important for partially trusted code, <paramref name="maxStackSize"/> is ignored if 
        /// it is greater than the default stack size.  No exception is thrown in theis case.
        /// </para>
        /// <para>
        /// This <b>defaults to 0</b> which generally means the stack size will be limited
        /// to <b>1 MiB for 32-bit</b> applications or <b>4 MiB for 64-bit</b> applications.
        /// </para>
        /// </param>
        /// <returns>The <see cref="Thread"/>.</returns>
        public static Thread StartThread(Action<object> action, object parameter, int maxStackSize = 0)
        {
            Covenant.Requires<ArgumentNullException>(action != null, nameof(action));
            Covenant.Requires<ArgumentException>(maxStackSize >= 0, nameof(maxStackSize));

            // Wrap the user's action with another action here so we can
            // cast the parameter to required type.

            var thread = new Thread(new ParameterizedThreadStart(action), maxStackSize: maxStackSize);

            thread.Start(parameter);

            return thread;
        }

        /// <summary>
        /// Starts a new <see cref="Thread"/> to perform a parameterized action with
        /// a typed parameter.
        /// </summary>
        /// <typeparam name="TParam">Identifies the type of the thread action parameter.</typeparam>
        /// <param name="action">The action to be performed.</param>
        /// <param name="parameter">The parameter to be passed to the thread action.</param>
        /// <param name="maxStackSize">
        /// <para>
        /// Optionally specifies the maximum stack size, in bytes, to be used by the thread, or 
        /// 0 to use the default maximum stack size specified in the header for the executable.
        /// Important for partially trusted code, <paramref name="maxStackSize"/> is ignored if 
        /// it is greater than the default stack size.  No exception is thrown in theis case.
        /// </para>
        /// <para>
        /// This <b>defaults to 0</b> which generally means the stack size will be limited
        /// to <b>1 MiB for 32-bit</b> applications or <b>4 MiB for 64-bit</b> applications.
        /// </para>
        /// </param>
        /// <returns>The <see cref="Thread"/>.</returns>
        public static Thread StartTypedThread<TParam>(Action<TParam> action, TParam parameter, int maxStackSize = 0)
        {
            Covenant.Requires<ArgumentNullException>(action != null, nameof(action));
            Covenant.Requires<ArgumentException>(maxStackSize >= 0, nameof(maxStackSize));

            // Wrap the user's action with another action here so we can
            // cast the parameter to required type.

            var thread = new Thread(new ParameterizedThreadStart(arg => action((TParam)arg)), maxStackSize: maxStackSize);

            thread.Start(parameter);

            return thread;
        }

        /// <summary>
        /// Verfies that an action does not throw an exception.
        /// </summary>
        /// <param name="action">The action.</param>
        /// <returns><c>true</c> if no exception was thrown.</returns>
        [Pure]
        public static bool DoesNotThrow(Action action)
        {
            Covenant.Requires<ArgumentNullException>(action != null, nameof(action));

            try
            {
                action();
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Verfies that an action does not throw a <typeparamref name="TException"/>.
        /// </summary>
        /// <param name="action">The action.</param>
        /// <typeparam name="TException">The exception type.</typeparam>
        /// <returns><c>true</c> if no exception was thrown.</returns>
        public static bool DoesNotThrow<TException>(Action action)
            where TException : Exception
        {
            Covenant.Requires<ArgumentNullException>(action != null, nameof(action));

            try
            {
                action();
                return true;
            }
            catch (TException)
            {
                return false;
            }
            catch
            {
                return true;
            }
        }

        /// <summary>
        /// Expands any embedded TAB <b>(\t)</b> characters in the string passed
        /// into spaces such that the tab stops will be formatted correctly.
        /// </summary>
        /// <param name="input">The input string.</param>
        /// <param name="tabStop">
        /// Optionally expands TABs into spaces when greater than zero or converts 
        /// a series of leading spaces into tabs if less than zero.  This defaults
        /// to <b>4</b>.
        /// </param>
        /// <returns>The expanded string.</returns>
        /// <remarks>
        /// <note>
        /// If the string passed includes line ending characters (CR or LF) then 
        /// the output will include line endings for every line, including the
        /// last one.
        /// </note>
        /// <para>
        /// A positive <paramref name="tabStop"/> does what you'd expect by converting
        /// spaces in the string into TABs such that the tab stops align to the value
        /// passed.  This works a bit differently for negative values.
        /// </para>
        /// <para>
        /// A negative <paramref name="tabStop"/> indicates that leading spaces in each
        /// line will be converted into TABs.  A value of -1 indicates that each leading
        /// two spaces will bve converted into a TAB, a value of -2 indicates that each
        /// leading 2 spaces will be converted into a TAB, and so on.
        /// </para>
        /// <para>
        /// Conversion to TABs will cease when the first non space is ecountered and
        /// any odd number of spaces remaining will be included in the output.
        /// </para>
        /// </remarks>
        public static string ExpandTabs(string input, int tabStop = 4)
        {
            Covenant.Requires<ArgumentNullException>(input != null, nameof(input));

            if (tabStop == 0)
            {
                return input;
            }
            else if (tabStop == 1)
            {
                return input.Replace('\t', ' ');
            }

            var lineEndings = input.IndexOfAny(new char[] { '\r', '\n' }) >= 0;
            var sb          = new StringBuilder((int)(input.Length * 1.25));

            using (var reader = new StringReader(input))
            {
                if (tabStop > 0)
                {
                    foreach (var line in reader.Lines())
                    {
                        var position = 0;

                        foreach (var ch in line)
                        {
                            if (ch != '\t')
                            {
                                sb.Append(ch);
                                position++;
                            }
                            else
                            {
                                var spaceCount = tabStop - (position % tabStop);

                                if (spaceCount <= 0)
                                {
                                    // If the current position is on a tabstop then we
                                    // need to inject a full TAB worth of spaces.

                                    spaceCount = tabStop;
                                }

                                for (int i = 0; i < spaceCount; i++)
                                {
                                    sb.Append(' ');
                                }

                                position += spaceCount;
                            }
                        }

                        if (lineEndings)
                        {
                            sb.AppendLine();
                        }
                    }
                }
                else // tabStop < 0
                {
                    tabStop = -tabStop;

                    foreach (var line in reader.Lines())
                    {
                        var leadingSpaces = 0;

                        foreach (var ch in line)
                        {
                            if (ch == ' ')
                            {
                                leadingSpaces++;
                            }
                            else
                            {
                                break;
                            }
                        }

                        if (leadingSpaces == 0)
                        {
                            sb.Append(line);
                        }
                        else
                        {
                            var tabCount = leadingSpaces / tabStop;

                            if (tabCount == 0)
                            {
                                sb.Append(line);
                            }
                            else
                            {
                                sb.Append(new string('\t', tabCount));
                                sb.Append(line.Substring(tabCount * tabStop));
                            }
                        }

                        if (lineEndings)
                        {
                            sb.AppendLine();
                        }
                    }
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Waits for a boolean function to return <c>true</c>.
        /// </summary>
        /// <param name="predicate">The boolean predicate.</param>
        /// <param name="timeout">Optionally specifies the maximum time to wait.</param>
        /// <param name="pollInterval">Optionally specifies time to wait between each predicate call or <c>null</c> for a reasonable default.</param>
        /// <param name="timeoutMessage">Optionally overrides the <see cref="TimeoutException"/> message.</param>
        /// <exception cref="TimeoutException">Thrown if the never returned <c>true</c> before the timeout.</exception>
        /// <remarks>
        /// This method periodically calls <paramref name="predicate"/> until it
        /// returns <c>true</c> or <pararef name="timeout"/> exceeded.
        /// </remarks>
        public static void WaitFor(Func<bool> predicate, TimeSpan timeout, TimeSpan? pollInterval = null, string timeoutMessage = null)
        {
            var timeLimit = DateTimeOffset.UtcNow + timeout;

            if (!pollInterval.HasValue)
            {
                pollInterval = TimeSpan.FromMilliseconds(250);
            }

            while (true)
            {
                if (predicate())
                {
                    return;
                }

                Thread.Sleep(pollInterval.Value);

                if (DateTimeOffset.UtcNow >= timeLimit)
                {
                    throw new TimeoutException(timeoutMessage ?? "Timeout waiting for the predicate to return TRUE.");
                }
            }
        }

        /// <summary>
        /// Asynchronously waits for a boolean function to return <c>true</c>.
        /// </summary>
        /// <param name="predicate">The boolean predicate.</param>
        /// <param name="timeout">Optionally specifies the maximum time to wait.</param>
        /// <param name="pollInterval">Optionally specifies time to wait between each predicate call or <c>null</c> for a reasonable default.</param>
        /// <param name="timeoutMessage">Optionally overrides the <see cref="TimeoutException"/> message.</param>
        /// <param name="cancellationToken">Optionally specifies a <see cref="CancellationToken"/>.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <exception cref="TimeoutException">Thrown if the never returned <c>true</c> before the timeout.</exception>
        /// <remarks>
        /// This method periodically calls <paramref name="predicate"/> until it
        /// returns <c>true</c> or <pararef name="timeout"/> exceeded.
        /// </remarks>
        public static async Task WaitForAsync(
            Func<Task<bool>>    predicate, 
            TimeSpan timeout,   TimeSpan? pollInterval = null, 
            string              timeoutMessage         = null, 
            CancellationToken   cancellationToken      = default)
        {
            await SyncContext.Clear;

            var timeLimit = DateTimeOffset.UtcNow + timeout;

            if (!pollInterval.HasValue)
            {
                pollInterval = TimeSpan.FromMilliseconds(250);
            }

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (await predicate())
                {
                    return;
                }

                await Task.Delay(pollInterval.Value);

                if (DateTimeOffset.UtcNow >= timeLimit)
                {
                    throw new TimeoutException(timeoutMessage ?? "Timeout waiting for the predicate to return TRUE.");
                }
            }
        }

        /// <summary>
        /// Compares two <c>null</c> or non-<c>null</c> enumerable sequences for equality.
        /// </summary>
        /// <typeparam name="T">The enumerable item type.</typeparam>
        /// <param name="sequence1">The first list or <c>null</c>.</param>
        /// <param name="sequence2">The second list or <c>null</c>.</param>
        /// <returns><c>true</c> if the sequences have matching elements.</returns>
        /// <remarks>
        /// <note>
        /// This method is capable of comparing <c>null</c> arguments and also
        /// uses <see cref="object.Equals(object, object)"/> to compare individual 
        /// elements.
        /// </note>
        /// </remarks>
        public static bool SequenceEqual<T>(IEnumerable<T> sequence1, IEnumerable<T> sequence2)
        {
            var isNull1 = sequence1 == null;
            var isNull2 = sequence2 == null;

            if (isNull1 != isNull2)
            {
                return false;
            }

            if (isNull1)
            {
                return true; // Both sequences must be null
            }

            // Both sequences are not null.

            var enumerator1 = sequence1.GetEnumerator();
            var enumerator2 = sequence2.GetEnumerator();

            while (true)
            {
                var gotNext1 = enumerator1.MoveNext();
                var gotNext2 = enumerator2.MoveNext();

                if (gotNext1 != gotNext2)
                {
                    return false;   // The sequences have different numbers of items
                }

                if (!gotNext1)
                {
                    return true;    // We reached the end of both sequences without a difference
                }

                if (!object.Equals(enumerator1.Current, enumerator2.Current))
                {
                    return false;
                }
            }
        }

        /// <summary>
        /// Compares two <c>null</c> or non-<c>null</c> lists for equality.
        /// </summary>
        /// <typeparam name="T">The enumerable item type.</typeparam>
        /// <param name="list1">The first list or <c>null</c>.</param>
        /// <param name="list2">The second list or <c>null</c>.</param>
        /// <returns><c>true</c> if the sequences have matching elements.</returns>
        /// <remarks>
        /// <note>
        /// This method is capable of comparing <c>null</c> arguments and also
        /// uses <see cref="object.Equals(object, object)"/> to compare 
        /// individual elements.
        /// </note>
        /// </remarks>
        public static bool SequenceEqual<T>(IList<T> list1, IList<T> list2)
        {
            var isNull1 = list1 == null;
            var isNull2 = list2 == null;

            if (isNull1 != isNull2)
            {
                return false;
            }

            if (isNull1)
            {
                return true; // Both lists must be null
            }

            // Both lists are not null.

            if (list1.Count != list2.Count)
            {
                return false;
            }

            for (int i = 0; i < list1.Count; i++)
            {
                if (!object.Equals(list1[i], list2[i]))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Asynchronously waits for all of the <see cref="Task"/>s passed to complete.
        /// </summary>
        /// <param name="tasks">The tasks to wait on.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task WaitAllAsync(IEnumerable<Task> tasks)
        {
            await SyncContext.Clear;

            foreach (var task in tasks)
            {
                await task;
            }
        }

        /// <summary>
        /// Asynchronously waits for all of the <see cref="Task"/>s passed to complete.
        /// </summary>
        /// <param name="tasks">The tasks to wait on.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task WaitAllAsync(params Task[] tasks)
        {
            await SyncContext.Clear;

            foreach (var task in tasks)
            {
                await task;
            }
        }

        /// <summary>
        /// Waits for all of the threads passed to complete.  This method does nothing
        /// when <paramref name="threads"/> is <c>null</c> or empty.  Also and <c>null</c>
        /// threads passed will be ignored.
        /// </summary>
        /// <param name="threads">The threads being waited on.</param>
        public static void WaitAll(IEnumerable<Thread> threads)
        {
            if (threads == null)
            {
                return;
            }

            foreach (var thread in threads)
            {
                if (thread != null)
                {
                    thread.Join();
                }
            }
        }

        /// <summary>
        /// Asynchronously waits for all of the <see cref="Task"/>s passed to complete.
        /// </summary>
        /// <param name="tasks">Specifies the tasks being waited on..</param>
        /// <param name="timeout">Optionally specifies a timeout.</param>
        /// <param name="cancellationToken">Optionally a cancellation token.</param>
        /// <param name="timeoutMessage">
        /// Optionally specifies a message to be included in any <see cref="TimeoutException"/>
        /// thrown to help the what failed.
        /// </param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <exception cref="TimeoutException">Thrown if the <paramref name="timeout"/> was exceeded.</exception>
        public static async Task WaitAllAsync(
            IEnumerable<Task>   tasks, 
            TimeSpan?           timeout           = null, 
            string              timeoutMessage    = null,
            CancellationToken   cancellationToken = default)
        {
            await SyncContext.Clear;

            if (!timeout.HasValue)
            {
                timeout = TimeSpan.FromDays(365); // Use an effectively infinite timeout
            }

            var stopwatch = new Stopwatch();

            stopwatch.Start();

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var isCompleted = true;

                foreach (var task in tasks)
                {
                    if (!task.IsCompleted)
                    {
                        isCompleted = false;
                        break;
                    }
                }

                if (isCompleted)
                {
                    return;
                }

                if (stopwatch.Elapsed >= timeout)
                {
                    throw new TimeoutException(timeoutMessage);
                }

                await Task.Delay(250);
            }
        }

        /// <summary>
        /// Compares the two Newtonsoft JSON.NET <see cref="JToken"/> instances along
        /// with their decendants for equality.  This is an alternative to <see cref="JToken.EqualityComparer"/> 
        /// which seems to have some problems, as outlined in the remarks.
        /// </summary>
        /// <param name="token1">The first token.</param>
        /// <param name="token2">The second token.</param>
        /// <returns><c>true</c> if the tokens are to be considered as equal.</returns>
        public static bool JTokenEquals(JToken token1, JToken token2)
        {
            var token1IsNull = token1 == null;
            var token2IsNull = token2 == null;

            if (token1IsNull && token2IsNull)
            {
                return true;
            }
            else if (token1IsNull != token2IsNull)
            {
                return false;
            }

            if (token1.Type != token2.Type)
            {
                // This is the secret sauce.

                string reference1;
                string reference2;

                switch (token1.Type)
                {
                    case JTokenType.None:
                    case JTokenType.Null:
                    case JTokenType.Undefined:

                        reference1 = null;
                        break;

                    case JTokenType.Date:
                    case JTokenType.Guid:
                    case JTokenType.String:
                    case JTokenType.TimeSpan:
                    case JTokenType.Uri:

                        reference1 = (string)token1;
                        break;

                    default:

                        return false;
                }

                switch (token2.Type)
                {
                    case JTokenType.None:
                    case JTokenType.Null:
                    case JTokenType.Undefined:

                        reference2 = null;
                        break;

                    case JTokenType.Date:
                    case JTokenType.Guid:
                    case JTokenType.String:
                    case JTokenType.TimeSpan:
                    case JTokenType.Uri:

                        reference2 = (string)token1;
                        break;

                    default:

                        return false;
                }

                return reference1 == reference2;
            }

            switch (token1.Type)
            {
                case JTokenType.None:
                case JTokenType.Null:
                case JTokenType.Undefined:

                    return true;

                case JTokenType.Object:

                    var object1 = (JObject)token1;
                    var object2 = (JObject)token2;
                    var propertyCount = object1.Properties().Count();

                    if (propertyCount != object2.Properties().Count())
                    {
                        return false;
                    }

                    if (propertyCount == 0)
                    {
                        return true;
                    }

                    var propertyEnumerator1 = object1.Properties().GetEnumerator();
                    var propertyEnumerator2 = object2.Properties().GetEnumerator();

                    for (int i = 0; i < propertyCount; i++)
                    {
                        propertyEnumerator1.MoveNext();
                        propertyEnumerator2.MoveNext();

                        if (!JTokenEquals(propertyEnumerator1.Current, propertyEnumerator2.Current))
                        {
                            return false;
                        }
                    }

                    return true;

                case JTokenType.Array:

                    var array1 = (JArray)token1;
                    var array2 = (JArray)token2;
                    var elementCount = array1.Children().Count();

                    if (elementCount != array2.Children().Count())
                    {
                        return false;
                    }

                    if (elementCount == 0)
                    {
                        return true;
                    }

                    var arrayEnumerator1 = array1.Children().GetEnumerator();
                    var arrayEnumerator2 = array2.Children().GetEnumerator();

                    for (int i = 0; i < elementCount; i++)
                    {
                        arrayEnumerator1.MoveNext();
                        arrayEnumerator2.MoveNext();

                        if (!JTokenEquals(arrayEnumerator1.Current, arrayEnumerator2.Current))
                        {
                            return false;
                        }
                    }

                    return true;

                case JTokenType.Property:

                    var property1 = (JProperty)token1;
                    var property2 = (JProperty)token2;

                    if (property1.Name != property2.Name)
                    {
                        return false;
                    }

                    return JTokenEquals(property1.Value, property2.Value);

                default:
                case JTokenType.Integer:
                case JTokenType.Float:
                case JTokenType.String:
                case JTokenType.Boolean:
                case JTokenType.Date:
                case JTokenType.Bytes:
                case JTokenType.Guid:
                case JTokenType.Uri:
                case JTokenType.TimeSpan:
                case JTokenType.Comment:
                case JTokenType.Constructor:
                case JTokenType.Raw:

                    return token1.Equals(token2);
            }
        }

        /// <summary>
        /// Removes a <b>file://</b> scheme from the path URI if this is scheme
        /// is present.  The result will be a valid file system path.
        /// </summary>
        /// <param name="path">The path/URI to be converted.</param>
        /// <returns>The file system path.</returns>
        /// <remarks>
        /// <note>
        /// <para>
        /// This method behaves slightly differently when running on Windows and
        /// when running on Unix/Linux.  On Windows, file URIs are absolute file
        /// paths of the form:
        /// </para>
        /// <code language="none">
        /// FILE:///C:/myfolder/myfile
        /// </code>
        /// <para>
        /// To convert this into a valid file system path this method strips the
        /// <b>file://</b> scheme <i>and</i> the following forward slash.  On
        /// Unix/Linux, file URIs will have the form:
        /// </para>
        /// <code language="none">
        /// FILE:///myfolder/myfile
        /// </code>
        /// <para>
        /// In this case, the forward shlash following the <b>file://</b> scheme
        /// is part of the file system path and will not be removed.
        /// </para>
        /// </note>
        /// </remarks>
        public static string StripFileScheme(string path)
        {
            if (!path.ToLowerInvariant().StartsWith("file://"))
            {
                return path;
            }

            return path.Substring(NeonHelper.IsWindows ? 8 : 7);
        }

        /// <summary>
        /// Determines whether two byte arrays contain the same values in the same order.
        /// </summary>
        /// <param name="v1">Byte array #1.</param>
        /// <param name="v2">Byte array #2.</param>
        /// <returns><c>true</c> if the arrays are equal.</returns>
        public static bool ArrayEquals(byte[] v1, byte[] v2)
        {
            if (object.ReferenceEquals(v1, v2))
            {
                return true;
            }

            var v1IsNull = v1 == null;
            var v2IsNull = v2 == null;

            if (v1IsNull != v2IsNull)
            {
                return false;
            }

            if (v1 == null)
            {
                return true;
            }

            if (v1.Length != v2.Length)
            {
                return false;
            }

            for (int i = 0; i < v1.Length; i++)
            {
                if (v1[i] != v2[i])
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Returns the serialization information for an enumeration type.
        /// </summary>
        /// <typeparam name="TEnum">The enumeration type.</typeparam>
        private static EnumMemberSerializationInfo GetEnumMembers<TEnum>()
            where TEnum : struct, Enum
        {
            return GetEnumMembers(typeof(TEnum));
        }

        /// <summary>
        /// Returns the serialization information for an enumeration type.
        /// </summary>
        /// <param name="type">The enumeration type.</param>
        private static EnumMemberSerializationInfo GetEnumMembers(Type type)
        {
            Covenant.Requires<ArgumentNullException>(type != null, nameof(type));
            Covenant.Requires<ArgumentException>(type.IsEnum, nameof(type));

            lock (typeToEnumMemberInfo)
            {
                if (typeToEnumMemberInfo.TryGetValue(type, out var info))
                {
                    return info;
                }
            }

            // We don't have a cached [EnumMemberSerializationInfo] for this
            // enumeration type yet, so we're going to build one outside
            // of the lock, add, and then return it.
            //
            // There's a slight chance that another call will do the same
            // thing in parallel once for any given enum type, but we'll
            // handle this just fine using the indexer assignment below.

            var newInfo = new EnumMemberSerializationInfo();

            foreach (var member in type.GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                var enumMember = member.GetCustomAttribute<EnumMemberAttribute>();

                if (enumMember != null)
                {
                    var ordinal = Convert.ToInt64(member.GetRawConstantValue());

                    newInfo.EnumToStrings[enumMember.Value] = ordinal;
                    newInfo.EnumToOrdinals[ordinal] = enumMember.Value;
                }
            }

            lock (typeToEnumMemberInfo)
            {
                typeToEnumMemberInfo[typeof(Enum)] = newInfo;
            }

            return newInfo;
        }

        /// <summary>
        /// Type-safe <c>enum</c> parser that also honors any <see cref="EnumMemberAttribute"/>
        /// decorating the enumeration values.  This is case insensitive.
        /// </summary>
        /// <typeparam name="TEnum">The enumeration type.</typeparam>
        /// <param name="input">The input string.</param>
        /// <param name="defaultValue">
        /// Optionally specifies the value to be returned if the input cannot
        /// be parsed instead of throwing an exception.
        /// </param>
        /// <returns>The parsed value.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="input"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException">Thrown if <paramref name="input"/> is not valid.</exception>
        public static TEnum ParseEnum<TEnum>(string input, TEnum? defaultValue = null)
            where TEnum : struct, Enum
        {
            if (defaultValue.HasValue && string.IsNullOrEmpty(input))
            {
                return defaultValue.Value;
            }

            // Try parsing the enumeration using the standard mechanism.
            // Note that this does not honor any [EnumMember] attributes.

            if (Enum.TryParse<TEnum>(input, true, out var value))
            {
                return value;
            }

            // That didn't work, so we'll use a cached [EnumMember]
            // map for the type.

            var info = GetEnumMembers<TEnum>();

            if (info.EnumToStrings.TryGetValue(input, out var value1))
            {
                return (TEnum)Enum.ToObject(typeof(TEnum), value1);
            }
            else
            {
                if (defaultValue.HasValue)
                {
                    return defaultValue.Value;
                }

                throw new ArgumentException($"[{input}] is not a valid [{typeof(TEnum).Name}] value.");
            }
        }

        /// <summary>
        /// Type-safe <c>enum</c> parser that also honors any <see cref="EnumMemberAttribute"/>
        /// decorating the enumeration values.  This is case insensitive.
        /// </summary>
        /// <typeparam name="TEnum">The enumeration type.</typeparam>
        /// <param name="input">The input string.</param>
        /// <param name="output">Returns as the parsed value.</param>
        /// <returns><c>true</c> if the value was parsed.</returns>
        public static bool TryParse<TEnum>(string input, out TEnum output)
            where TEnum : struct, Enum
        {
            var info = GetEnumMembers<TEnum>();

            if (info.EnumToStrings.TryGetValue(input, out var value1))
            {
                output = (TEnum)Enum.ToObject(typeof(TEnum), value1);

                return true;
            }

            // Try parsing the enumeration using the standard mechanism.
            // Note that this does not honor any [EnumMember] attributes.

            if (Enum.TryParse<TEnum>(input, true, out output))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// <c>enum</c> parser that also honors any <see cref="EnumMemberAttribute"/>
        /// decorating the enumeration values.  This is case insensitive.
        /// </summary>
        /// <param name="type">The enumeration type.</param>
        /// <param name="input">The input string.</param>
        /// <param name="output">Returns as the parsed value.</param>
        /// <returns><c>true</c> if the value was parsed.</returns>
        public static bool TryParseEnum(Type type, string input, out object output)
        {
            var info = GetEnumMembers(type);

            if (info.EnumToStrings.TryGetValue(input, out var value1))
            {
                output = Enum.ToObject(type, value1);

                return true;
            }

            // Try parsing the enumeration using the standard mechanism.
            // Note that this does not honor any [EnumMember] attributes.

            try
            {
                output = Enum.Parse(type, input, ignoreCase: true);
                return true;
            }
            catch
            {
                output = null;
                return false;
            }
        }

        /// <summary>
        /// <c>enum</c> parser that also honors any <see cref="EnumMemberAttribute"/>
        /// decorating the enumeration values.  This is case insensitive.
        /// </summary>
        /// <typeparam name="TEnum">Specifies the enumeration type.</typeparam>
        /// <param name="input">The input string.</param>
        /// <param name="output">Returns as the parsed value.</param>
        /// <returns><c>true</c> if the value was parsed.</returns>
        public static bool TryParseEnum<TEnum>(string input, out TEnum output)
        {
            var type = typeof(TEnum);
            var info = GetEnumMembers(type);

            if (info.EnumToStrings.TryGetValue(input, out var value1))
            {
                output = (TEnum)Enum.ToObject(type, value1);

                return true;
            }

            // Try parsing the enumeration using the standard mechanism.
            // Note that this does not honor any [EnumMember] attributes.

            try
            {
                output = (TEnum)Enum.Parse(type, input, ignoreCase: true);

                return true;
            }
            catch
            {
                output = default(TEnum);

                return false;
            }
        }

        /// <summary>
        /// Type-safe <c>enum</c> serializer that also honors any <see cref="EnumMemberAttribute"/>
        /// decorating the enumeration values.
        /// </summary>
        /// <typeparam name="TEnum">The enumeration type.</typeparam>
        /// <param name="input">The input value.</param>
        /// <returns>The deserialized value.</returns>
        public static string EnumToString<TEnum>(TEnum input)
            where TEnum : struct, Enum
        {
            var info = GetEnumMembers<TEnum>();

            if (info.EnumToOrdinals.TryGetValue(Convert.ToInt64(input), out var value))
            {
                return value;
            }
            else
            {
                return input.ToString();
            }
        }

        /// <summary>
        /// Type-safe <c>enum</c> serializer that also honors any <see cref="EnumMemberAttribute"/>
        /// decorating the enumeration values.
        /// </summary>
        /// <param name="type">The enumeration type.</param>
        /// <param name="input">The input value.</param>
        /// <returns>The deserialized value.</returns>
        public static string EnumToString(Type type, object input)
        {
            Covenant.Requires<ArgumentNullException>(type != null, nameof(type));
            Covenant.Requires<ArgumentNullException>(input != null, nameof(input));
            Covenant.Requires<ArgumentException>(type.IsEnum, nameof(type));

            var info = GetEnumMembers(type);

            if (info.EnumToOrdinals.TryGetValue(Convert.ToInt64(input), out var value))
            {
                return value;
            }
            else
            {
                return input.ToString();
            }
        }

        /// <summary>
        /// Returns the value names for an enumeration type.  This is similar to <see cref="Enum.GetNames(Type)"/>
        /// but also honors value names customized via <see cref="EnumMemberAttribute"/>.
        /// </summary>
        /// <returns>The array of value names.</returns>
        public static string[] GetEnumNames<TEnum>()
            where TEnum : struct, Enum
        {
            var type  = typeof(TEnum);
            var names = new List<string>();

            foreach (TEnum value in Enum.GetValues(type))
            {
                names.Add(NeonHelper.EnumToString(type, value));
            }

            return names.ToArray();
        }

        /// <summary>
        /// Encodes a byte array into a form suitable for using as a URI path
        /// segment or query parameter.
        /// </summary>
        /// <param name="input">The byte array.</param>
        /// <returns>The encoded string.</returns>
        /// <exception cref="NullReferenceException">Thrown if <paramref name="input"/> is <c>null</c>.</exception>
        public static string UrlTokenEncode(byte[] input)
        {
            if (input == null)
            {
                throw new ArgumentNullException("input");
            }

            if (input.Length < 1)
            {
                return String.Empty;
            }

            string  base64Str = null;
            int     endPos = 0;
            char[]  base64Chars = null;

            //------------------------------------------------------
            // Step 1: Do a Base64 encoding

            base64Str = Convert.ToBase64String(input);

            if (base64Str == null)
            {
                return null;
            }

            //------------------------------------------------------
            // Step 2: Find how many padding chars are present in the end

            for (endPos = base64Str.Length; endPos > 0; endPos--)
            {
                if (base64Str[endPos - 1] != '=') // Found a non-padding char!
                {
                    break; // Stop here
                }
            }

            //------------------------------------------------------
            // Step 3: Create char array to store all non-padding chars,
            //      plus a char to indicate how many padding chars are needed

            base64Chars         = new char[endPos + 1];
            base64Chars[endPos] = (char)((int)'0' + base64Str.Length - endPos); // Store a char at the end, to indicate how many padding chars are needed

            //------------------------------------------------------
            // Step 3: Copy in the other chars. Transform the "+" to "-", and "/" to "_"

            for (int i = 0; i < endPos; i++)
            {
                var ch = base64Str[i];

                switch (ch)
                {
                    case '+':

                        base64Chars[i] = '-';
                        break;

                    case '/':

                        base64Chars[i] = '_';
                        break;

                    case '=':

                        base64Chars[i] = ch;
                        break;

                    default:

                        base64Chars[i] = ch;
                        break;
                }
            }

            return new string(base64Chars);
        }

        /// <summary>
        /// Decodes a string encoded by <see cref="UrlTokenEncode(byte[])"/> back
        /// into a byte array.
        /// </summary>
        /// <param name="input">The input string.</param>
        /// <returns>The decoded bytes.</returns>
        /// <exception cref="NullReferenceException">Thrown if <paramref name="input"/> is <c>null</c>.</exception>
        public static byte[] UrlTokenDecode(string input)
        {
            if (input == null)
            {
                throw new ArgumentNullException("input");
            }

            var length = input.Length;

            if (length < 1)
            {
                return Array.Empty<byte>();
            }

            //------------------------------------------------------
            // Step 1: Calculate the number of padding chars to append to this string.
            //         The number of padding chars to append is stored in the last char of the string.

            int numPadChars = (int)input[length - 1] - (int)'0';

            if (numPadChars < 0 || numPadChars > 10)
            {
                return null;
            }

            //------------------------------------------------------
            // Step 2: Create array to store the chars (not including the last char)
            //          and the padding chars

            var base64Chars = new char[length - 1 + numPadChars];

            //------------------------------------------------------
            // Step 3: Copy in the chars. Transform the "-" to "+", and "*" to "/"

            for (int i = 0; i < length - 1; i++)
            {
                char c = (char)input[i];

                switch (c)
                {
                    case '-':

                        base64Chars[i] = '+';
                        break;

                    case '_':

                        base64Chars[i] = '/';
                        break;

                    default:

                        base64Chars[i] = c;
                        break;
                }
            }

            //------------------------------------------------------
            // Step 4: Add padding chars

            for (int i = length - 1; i < base64Chars.Length; i++)
            {
                base64Chars[i] = '=';
            }

            // Do the actual conversion

            return Convert.FromBase64CharArray(base64Chars, 0, base64Chars.Length);
        }

        /// <summary>
        /// Encodes a byte array using <b>Base64Url</b> encoding as specifed here: <a href="">RFC 4648</a>
        /// </summary>
        /// <param name="bytes">The input byte array.</param>
        /// <param name="retainPadding">
        /// Optionally onverts any '=' characters padding into escaped "%3D", otherwise
        /// any padding will be omitted from the output.
        /// </param>
        /// <returns>The Base64Url encoded string.</returns>
        public static string Base64UrlEncode(byte[] bytes, bool retainPadding = false)
        {
            // $todo(jefflill): 
            //
            // This would be more efficient as a native implemenation rather than
            // converting to standard Base64 first.

            Covenant.Requires<ArgumentNullException>(bytes != null, nameof(bytes));

            var encoded = Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_');

            if (retainPadding)
            {
                encoded = encoded.Replace("=", "%3D");
            }
            else
            {
                encoded = encoded.TrimEnd('=');
            }

            return encoded.Replace('+', '-').Replace('/', '_');
        }

        private readonly static char[] oneBase64Pad  = "=".ToCharArray();
        private readonly static char[] twoBase64Pads = "==".ToCharArray();

        /// <summary>
        /// Decodes a base64url encoded string.  This is a URL and filename safe base-64 based 
        /// encoding scheme: <a href="https://tools.ietf.org/html/rfc4648#section-5">RFC6448</a>.
        /// </summary>
        /// <param name="encoded">The encoded string.</param>
        /// <returns>The decoded bytes.</returns>
        public static byte[] Base64UrlDecode(string encoded)
        {
            // $todo(jefflill): 
            //
            // This would be more efficient as a native implemenation rather than
            // converting from standard Base64 and messing around with character
            // lists.

            Covenant.Requires<ArgumentNullException>(encoded != null, nameof(encoded));

            var chars = new List<char>(Uri.UnescapeDataString(encoded).ToCharArray());

            for (int i = 0; i < chars.Count; ++i)
            {
                if (chars[i] == '_')
                {
                    chars[i] = '/';
                }
                else if (chars[i] == '-')
                {
                    chars[i] = '+';
                }
            }

            switch (chars.Count % 4)
            {
                case 2:
                    chars.AddRange(twoBase64Pads);
                    break;
                case 3:
                    chars.AddRange(oneBase64Pad);
                    break;
            }

            var array = chars.ToArray();

            return Convert.FromBase64CharArray(array, 0, array.Length);
        }

        /// <summary>
        /// Converts the string passed into base64 string.
        /// </summary>
        /// <param name="value">The plaintext string to be encoded (cannot be <c>null</c>).</param>
        /// <param name="encoding">
        /// Optionally specifies the encoding to use to convert the input to bytes 
        /// before base64 encoding it.  This defaults to <see cref="Encoding.UTF8"/>.
        /// </param>
        /// <returns>The converted base-64 string.</returns>
        public static string ToBase64(string value, Encoding encoding = null)
        {
            Covenant.Requires<ArgumentNullException>(value != null, nameof(value));

            encoding ??= Encoding.UTF8;

            return Convert.ToBase64String(encoding.GetBytes(value));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="encodedValue">The base64 encoded input.</param>
        /// <param name="encoding">
        /// Optionally specifies the encoding to use to convert the decoded bytes 
        /// to the result.  This defaults to <see cref="Encoding.UTF8"/>.
        /// </param>
        /// <returns>The converted string.</returns>
        public static string FromBase64(string encodedValue, Encoding encoding = null)
        {
            Covenant.Requires<ArgumentNullException>(encodedValue != null, nameof(encodedValue));
            
            encoding ??= Encoding.UTF8;

            return encoding.GetString(Convert.FromBase64String(encodedValue));
        }

        /// <summary>
        /// Returns the fully qualified path to the folder where the executable resides.
        /// This will include the terminating "\".
        /// </summary>
        /// <returns>Path to the folder holding the executable</returns>
        public static string GetBaseDirectory()
        {
            var directory = AppContext.BaseDirectory;

            if (directory.EndsWith("/"))
            {
                directory += "/";
            }

            return directory;
        }

        /// <summary>
        /// Returns the fully qualified path to the folder holding the
        /// assembly passed (includes the terminating "\").
        /// </summary>
        /// <param name="assembly">The assembly.</param>
        /// <returns>Path to the folder holding the assembly.</returns>
        [Obsolete("Avoid this because it is not compatable with single-file executables.")]
        public static string GetAssemblyFolder(Assembly assembly)
        {
            // Get the path to the directory hosting the assembly by
            // stripping off the file URI scheme if present and the
            // assembly's file name.

            string  path;
            int     pos;

            path = NeonHelper.StripFileScheme(assembly.Location);

            pos = path.LastIndexOfAny(new char[] { '/', '\\' });
            if (pos == -1)
                throw new InvalidOperationException("Helper.GetAssemblyFolder() works only for assemblies loaded from disk.");

            return path.Substring(0, pos + 1);
        }

        /// <summary>
        /// Returns the fully qualified path to the assembly file.
        /// </summary>
        /// <param name="assembly">The assembly.</param>
        /// <returns>The assembly's path.</returns>
        [Obsolete("Avoid this because it is not compatable with single-file executables.")]
        public static string GetAssemblyPath(Assembly assembly)
        {
            // Get the path to the directory hosting the assembly by
            // stripping off the file URI scheme if present and the
            // assembly's file name.

            return NeonHelper.StripFileScheme(assembly.Location);
        }

        /// <summary>
        /// Returns the fully qualified path the entry assembly for the current process.
        /// </summary>
        /// <returns>The entry assembly file path.</returns>
        [Obsolete("Avoid this because it is not compatable with single-file executables.")]
        public static string GetEntryAssemblyPath()
        {
            return GetAssemblyPath(Assembly.GetEntryAssembly());
        }

        /// <summary>
        /// Deserializes JSON or YAML text using, optionally requiring strict mapping of input properties to the target type.
        /// </summary>
        /// <typeparam name="T">The desired output type.</typeparam>
        /// <param name="input">The input text (JSON or YAML).</param>
        /// <param name="strict">Optionally require that all input properties map to <typeparamref name="T"/> properties.</param>
        /// <returns>The parsed <typeparamref name="T"/>.</returns>
        /// <remarks>
        /// <note>
        /// This method works by looking for leading '{' or '[' as the first non-whitespace character
        /// in the string to detect whether the input is JSON.  The method assumes YAML otherwise.
        /// </note>
        /// </remarks>
        public static T JsonOrYamlDeserialize<T>(string input, bool strict = false)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrWhiteSpace(input), nameof(input));

            var trimmed = input.TrimStart();

            switch (trimmed[0])
            {
                case '{':
                case '[':

                    return NeonHelper.JsonDeserialize<T>(input, strict);

                default:

                    return NeonHelper.YamlDeserialize<T>(input, strict);
            }
        }

        /// <summary>
        /// Attempts to parse a boolean from common literals.
        /// </summary>
        /// <param name="input">The input string being parsed.</param>
        /// <param name="value">Returns as the parsed value on success.</param>
        /// <returns><c>true</c> on success.</returns>
        /// <remarks>
        /// <para>
        /// This method recognizes the following case insensitive literals:
        /// </para>
        /// <list type="table">
        /// <item>
        /// <term><c>false</c></term>
        /// <description>
        /// <para><b>0</b></para>
        /// <para><b>off</b></para>
        /// <para><b>no</b></para>
        /// <para><b>disabled</b></para>
        /// <para><b>false</b></para>
        /// </description>
        /// </item>
        /// <item>
        /// <term><c>true</c></term>
        /// <description>
        /// <para><b>1</b></para>
        /// <para><b>on</b></para>
        /// <para><b>yes</b></para>
        /// <para><b>enabled</b></para>
        /// <para><b>true</b></para>
        /// </description>
        /// </item>
        /// </list>
        /// </remarks>
        public static bool TryParseBool(string input, out bool value)
        {
            value = default(bool);

            if (string.IsNullOrEmpty(input))
            {
                return false;
            }

            switch (input.ToLowerInvariant())
            {
                case "0":
                case "off":
                case "no":
                case "disabled":
                case "false":

                    value = false;
                    return true;

                case "1":
                case "on":
                case "yes":
                case "enabled":
                case "true":

                    value = true;
                    return true;

                default:

                    return false;
            }
        }

        /// <summary>
        /// Parses common boolean literals.
        /// </summary>
        /// <param name="input">The input string being parsed.</param>
        /// <returns>The parsed output.</returns>
        /// <exception cref="FormatException">Thrown if the value is not valid.</exception>
        /// <remarks>
        /// <para>
        /// This method recognizes the following case insensitive literals:
        /// </para>
        /// <list type="table">
        /// <item>
        /// <term><c>false</c></term>
        /// <description>
        /// <para><b>0</b></para>
        /// <para><b>off</b></para>
        /// <para><b>no</b></para>
        /// <para><b>disabled</b></para>
        /// <para><b>false</b></para>
        /// </description>
        /// </item>
        /// <item>
        /// <term><c>true</c></term>
        /// <description>
        /// <para><b>1</b></para>
        /// <para><b>on</b></para>
        /// <para><b>yes</b></para>
        /// <para><b>enabled</b></para>
        /// <para><b>true</b></para>
        /// </description>
        /// </item>
        /// </list>
        /// </remarks>
        public static bool ParseBool(string input)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(input), nameof(input));

            if (TryParseBool(input, out var value))
            {
                return value;
            }
            else
            {
                throw new FormatException($"[{input}] is not a valid boolean literal (1/0, on/off, yes/no, true/false, enabled/disabled).");
            }
        }

        /// <summary>
        /// Parses a nullable <see cref="bool"/>.
        /// </summary>
        /// <param name="input">
        /// The input string being parsed.  <c>null</c>, empty or <paramref name="input"/> == <b>"null"</b> 
        /// will return <c>null</c>.  Otherwise we'll expect either <b>"true"</b> or <b>"false"</b> or
        /// one of the other literals supported by <see cref="ParseBool(string)"/>.
        /// </param>
        /// <note>
        /// This method is case insensitive.
        /// </note>
        /// <returns><c>true</c>, <c>false</c>, or <c>null</c>.</returns>
        /// <exception cref="FormatException">Thrown for invalid input strings.</exception>
        public static bool? ParseNullableBool(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return null;
            }

            return ParseBool(input);
        }

        /// <summary>
        /// Attempts to parse a nullable <see cref="bool"/>.  <c>null</c>, empty or <paramref name="input"/> == <b>"null"</b> 
        /// will return <c>null</c>.  Otherwise we'll expect either <b>"true"</b> or <b>"false"</b> or
        /// one of the other literals supported by <see cref="ParseBool(string)"/>.
        /// </summary>
        /// <param name="input">The input string being parsed.</param>
        /// <param name="value">Returns as the parsed value.</param>
        /// <returns><c>true</c> if the input was parsed successfully.</returns>
        public static bool TryParseNullableBool(string input, out bool? value)
        {
            value = null;

            if (string.IsNullOrEmpty(input))
            {
                return true;
            }

            if (TryParseBool(input, out var v))
            {
                value = v;

                return true;
            }

            return false;
        }

        /// <summary>
        /// <b>HACK:</b> This method attempts to trim warnings generated by Ansible because
        /// it writes these warnings to STDOUT instead of STDERR.  This is super fragile.
        /// </summary>
        /// <param name="text">The text to be adjusted.</param>
        /// <returns>The adjusted text.</returns>
        public static string StripAnsibleWarnings(string text)
        {
            // $hack(jefflill):
            //
            // Ansible has recently made change where they write this warning out to STDOUT:
            // 
            //      [WARNING] Ansible is in a world writable directory...
            //      https://docs.ansible.com/ansible/devel/reference_appendices/config.html#cfg-in-world-writable-dir
            //
            // There's an issue surrounding this:
            //
            //      https://github.com/ansible/ansible/issues/42388

            var trimmed = text.TrimStart();

            if (trimmed.StartsWith("[WARNING]"))
            {
                var posLF = trimmed.IndexOf('\n');

                if (posLF != -1)
                {
                    text = trimmed.Substring(posLF + 1);
                }
            }

            return text;
        }

        /// <summary>
        /// Renders a <c>bool</c> value as either <b>true</b> or <b>false</b>
        /// (lowercase).
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns><b>true</b> or <b>false</b>,</returns>
        public static string ToBoolString(bool value)
        {
            return value ? "true" : "false";
        }

        /// <summary>
        /// Do nothing method that is used when you explicitly don't want to
        /// <c>await</c> a task and you don't want to see warning <b>CS4014</b>.
        /// </summary>
        /// <param name="task">The task.</param>
        public static void NoAwait(Task task)
        {
        }

        /// <summary>
        /// Used for implementing unit tests against the <see cref="OpenEditor(string)"/>
        /// method.  <see cref="OpenEditor(string)"/> will call this action when it's
        /// non-null passing the file path, rather than actually opening the file in
        /// an editor.  The handler can then simulate editing the file.
        /// </summary>
        public static Action<string> OpenEditorHandler { get; set; }

        /// <summary>
        /// Launches the platform text editor to create or edit a file.
        /// </summary>
        /// <param name="path">The file path.</param>
        /// <remarks>
        /// <para>
        /// This method will launch the editor specified in the <b>EDITOR</b>
        /// environment variable otherwise it will launch NotePad on Windows
        /// and Vim on Linux and OS/X.  <b>EDITOR</b> should be set to the
        /// command line used to launch the editor with special <b>$FILE</b> 
        /// parameter.  This will be replaced with the path to the file being 
        /// edited.
        /// </para>
        /// <note>
        /// We'll simply append the file path if <b>$FILE</b> isn't found in
        /// the <b>EDITOR</b> environment variable.
        /// </note>
        /// <para>
        /// This method will block until the editor is closed.
        /// </para>
        /// <note>
        /// For unit testing, you may set <see cref="OpenEditorHandler"/> to
        /// an action that will simulate editing the file.  This action will
        /// be called instead of actually opening an editor when set.
        /// </note>
        /// </remarks>
        public static void OpenEditor(string path)
        {
            if (OpenEditorHandler != null)
            {
                OpenEditorHandler(path);
                return;
            }

            // Quote the file path if necessary.

            if (path.Contains(' '))
            {
                path = $"\"{path}\"";
            }

            var editor = Environment.GetEnvironmentVariable("EDITOR");

            if (!string.IsNullOrEmpty(editor))
            {
                if (!editor.Contains("$FILE"))
                {
                    editor += $" {path}";
                }
                else
                {
                    editor = editor.Replace("$FILE", path);
                }

                // Extract the path to the executable.

                string executablePath;
                string args;

                if (editor.StartsWith("\""))
                {
                    var pos = editor.IndexOf('\"', 1);

                    if (pos == -1)
                    {
                        executablePath = editor;
                        args           = string.Empty;
                    }
                    else
                    {
                        executablePath = editor.Substring(0, pos);
                        args           = editor.Substring(pos + 1).Trim();
                    }
                }
                else
                {
                    var pos = editor.IndexOf(' ', 1);

                    if (pos == -1)
                    {
                        executablePath = editor;
                        args           = string.Empty;
                    }
                    else
                    {
                        executablePath = editor.Substring(0, pos);
                        args           = editor.Substring(pos + 1).Trim();
                    }
                }

                // Strip any quotes off the executable path.

                if (executablePath.StartsWith("\""))
                {
                    executablePath = executablePath.Substring(1);
                }

                if (executablePath.EndsWith("\""))
                {
                    executablePath = executablePath.Substring(0, executablePath.Length - 1);
                }

                if (IsWindows)
                {
                    // Special case NotePad++ on Windows by adding the [-multiInst]
                    // option if it's not already present.

                    if (executablePath.ToLowerInvariant().Contains("notepad++") && !args.Contains("-multiInst"))
                    {
                        args = "-multiInst " + args;
                    }
                }

                Execute(executablePath, args);
            }
            else if (IsWindows)
            {
                Execute("notepad.exe", new object[] { path });
            }
            else
            {
                Execute("vim", new object[] { path });
            }
        }

        /// <summary>
        /// <para>
        /// This method may be called to ensure that the <b>Neon.Common</b> assembly
        /// is required at compile in a project that doesn't reference <b>Neon.Common</b>.
        /// The method does nothing.
        /// </para>
        /// <note>
        /// A call to this is currently included by <b>Neon.ModelGen</b> to ensure that
        /// the enclosing project references <b>Neon.Common</b>.
        /// </note>
        /// </summary>
        public static void PackageReferenceToNeonCommonIsRequired()
        {
        }

        /// <summary>
        /// Uses reflection to locate a specific public, non-public, instance or static method on a type.
        /// </summary>
        /// <param name="type">The target type.</param>
        /// <param name="name">The method name.</param>
        /// <param name="parameterTypes">The parameter types.</param>
        /// <returns>The <see cref="MethodInfo"/>.</returns>
        /// <exception cref="MissingMethodException">Thrown if the method does not exist.</exception>
        public static MethodInfo GetMethod(Type type, string name, params Type[] parameterTypes)
        {
            Covenant.Requires<ArgumentNullException>(type != null, nameof(type));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name), nameof(name));
            Covenant.Requires<ArgumentNullException>(parameterTypes != null, nameof(parameterTypes));

            var method = type.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static, null, parameterTypes, null);

            if (method == null)
            {
                var sb = new StringBuilder();

                foreach (var parameterType in parameterTypes)
                {
                    sb.AppendWithSeparator(parameterType.FullName, ", ");
                }

                throw new MissingMethodException($"Cannot locate the [{type.FullName}.{name}({sb})] method.");
            }

            return method;
        }

        /// <summary>
        /// Uses reflection to locate a specific public or non-public constructor for a type.
        /// </summary>
        /// <param name="type">The target type.</param>
        /// <param name="parameterTypes">The parameter types.</param>
        /// <returns>The <see cref="MethodInfo"/>.</returns>
        /// <exception cref="MissingMethodException">Thrown if the method does not exist.</exception>
        public static ConstructorInfo GetConstructor(Type type, params Type[] parameterTypes)
        {
            Covenant.Requires<ArgumentNullException>(type != null, nameof(type));
            Covenant.Requires<ArgumentNullException>(parameterTypes != null, nameof(parameterTypes));

            var constructor = type.GetConstructor(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, parameterTypes, null);

            if (constructor == null)
            {
                var sb = new StringBuilder();

                foreach (var parameterType in parameterTypes)
                {
                    sb.AppendWithSeparator(parameterType.FullName, ", ");
                }

                throw new MissingMethodException($"Cannot locate the [{type.FullName}({sb})] constructor.");
            }

            return constructor;
        }

        /// <summary>
        /// Used to await a generic <see cref="Task{T}"/> and return its result as
        /// an <see cref="object"/>.  This is handy for situations where the task
        /// result type is unknown at compile time.
        /// </summary>
        /// <param name="task">The <see cref="Task{T}"/>.</param>
        /// <returns>The task result.</returns>
        public static async Task<object> GetTaskResultAsObjectAsync(Task task)
        {
            await SyncContext.Clear;
            Covenant.Requires<ArgumentNullException>(task != null, nameof(task));

            await task;

            var property = task.GetType().GetProperty("Result", BindingFlags.Public | BindingFlags.Instance);

            if (property == null || property.PropertyType.FullName == "System.Threading.Tasks.VoidTaskResult")
            {
                throw new ArgumentException("Task does not return a result.");
            }

            return property.GetValue(task);
        }

        /// <summary>
        /// Returns instances of the types that implement <see cref="IEnhancedJsonConverter"/> from the
        /// <b>Neon.Common</b> assembly.
        /// </summary>
        /// <returns>The list of converters.</returns>
        public static List<IEnhancedJsonConverter> GetEnhancedJsonConverters()
        {
            // Scan the [Neon.Common] assembly for JSON converters that implement [IEnhancedJsonConverter]
            // and initialize the [convertableTypes] hashset with the convertable types.  We'll need
            // this to be able pass "complex" types like [TimeSpan] to web services via route or the
            // query string.

            // $todo(jefflill):
            //
            // This limits us to support only JSON converters hosted by [Neon.Common].  At some point,
            // it might be nice if we could handle user provided converters as well.

            var helperAssembly = typeof(NeonHelper).Assembly;
            var converters     = new List<IEnhancedJsonConverter>();

            var types = helperAssembly.GetTypes();

            foreach (var type in helperAssembly.GetTypes())
            {
                if (type.Implements<IEnhancedJsonConverter>())
                {
                    converters.Add((IEnhancedJsonConverter)helperAssembly.CreateInstance(type.FullName));
                }
            }

            return converters;
        }

        private static string dockerCliPath     = null;
        private static string dockerComposePath = null;

        /// <summary>
        /// Returns the name of the Docker CLI execuable for the current platform.  This will
        /// be the fully qualified pathj to <b>docker.exe</b> on Windows and just <b>docker</b>
        /// on Linux and OS/X.
        /// </summary>
        /// <exception cref="FileNotFoundException">Thrown when the Docker client could  not be located.</exception>
        public static string DockerCli
        {
            get
            {
                if (dockerCliPath != null)
                {
                    return dockerCliPath;
                }

                if (IsWindows)
                {
                    // Docker Desktop has installed the CLI binary at different locations over time.  We'll
                    // probe the most recent locations first.

                    var potentialPaths =
                        new string[]
                        {
                            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"Docker\Docker\resources\bin\docker.exe"),
                            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), @"DockerDesktop\version-bin\docker.exe")
                        };

                    foreach (var path in potentialPaths)
                    {
                        if (File.Exists(path))
                        {
                            return dockerCliPath = path;
                        }
                    }

                    throw new FileNotFoundException("Cannot locate the docker CLI.");
                }
                else
                {
                    return dockerCliPath = "docker";
                }
            }
        }

        /// <summary>
        /// Returns the name of the Docker Compose CLI execuable for the current platform.  This will
        /// be <b>docker-compose.exe</b> on Windows and just <b>docker-compose</b> on Linux and OS/x.
        /// </summary>
        public static string DockerComposeCli
        {
            get
            {
                if (dockerComposePath != null)
                {
                    return dockerComposePath;
                }

                if (IsWindows)
                {
                    dockerComposePath = "docker-compose.exe";
                }
                else
                {
                    dockerComposePath = "docker-compose";
                }

                return dockerComposePath;
            }
        }

        /// <summary>
        /// Determines whether a <paramref name="value"/> is within <paramref name="expected"/> - <paramref name="maxDelta"/>
        /// and <paramref name="value"/> + <paramref name="maxDelta"/> inclusive.  This is useful for unit tests 
        /// where there might be an minor allowable variance due to clock skew, etc.
        /// </summary>
        /// <param name="expected">The expected value.</param>
        /// <param name="value">The value being tested.</param>
        /// <param name="maxDelta">The allowed variance.</param>
        /// <returns><c>true</c> when the two datetime values are within <paramref name="maxDelta"/> of each other.</returns>
        public static bool IsWithin(DateTime expected, DateTime value, TimeSpan maxDelta)
        {
            Covenant.Requires<ArgumentException>(maxDelta >= TimeSpan.Zero, nameof(maxDelta));

            var delta = value - expected;

            if (delta < TimeSpan.Zero)
            {
                delta = -delta;
            }

            return delta <= maxDelta;
        }

        /// <summary>
        /// Determines whether a <paramref name="value"/> is within <paramref name="expected"/> - <paramref name="maxDelta"/>
        /// and <paramref name="value"/> + <paramref name="maxDelta"/> inclusive.  This is useful for unit tests 
        /// where there might be an minor allowable variance due to clock skew, etc.
        /// </summary>
        /// <param name="expected">The expected value.</param>
        /// <param name="value">The value being tested.</param>
        /// <param name="maxDelta">The allowed variance.</param>
        /// <returns><c>true</c> when the two datetime values are within <paramref name="maxDelta"/> of each other.</returns>
        public static bool IsWithin(DateTimeOffset expected, DateTimeOffset value, TimeSpan maxDelta)
        {
            Covenant.Requires<ArgumentException>(maxDelta >= TimeSpan.Zero, nameof(maxDelta));

            var delta = value - expected;

            if (delta < TimeSpan.Zero)
            {
                delta = -delta;
            }

            return delta <= maxDelta;
        }

        /// <summary>
        /// Attempts to cast an object into a specific type.
        /// </summary>
        /// <typeparam name="TResult">The desired result type.</typeparam>
        /// <param name="value">The value to be cast.</param>
        /// <returns>The casted result.</returns>
        /// <exception cref="InvalidCastException">Thrown if the value could not be cast into the type.</exception>
        public static TResult CastTo<TResult>(object value)
        {
            return (TResult)value;
        }

        /// <summary>
        /// Computes the <see cref="int"/> number of partitions that would be required to divide a
        /// set of items where the number of items in each partition is limited.
        /// </summary>
        /// <param name="itemCount">The number of items to be partitioned.</param>
        /// <param name="partitionSize">The maximim number of items in any partition.</param>
        /// <returns>The number of partitions required.</returns>
        public static int PartitionCount(int itemCount, int partitionSize)
        {
            Covenant.Requires<ArgumentException>(itemCount >= 0, nameof(itemCount));
            Covenant.Requires<ArgumentException>(partitionSize > 0, nameof(partitionSize));

            var partitions = itemCount / partitionSize;

            if (itemCount % partitionSize != 0)
            {
                partitions++;
            }

            return partitions;
        }

        /// <summary>
        /// Computes the <see cref="uint"/> number of partitions that would be required to divide a
        /// set of items where the number of items in each partition is limited.
        /// </summary>
        /// <param name="itemCount">The number of items to be partitioned.</param>
        /// <param name="partitionSize">The maximim number of items in any partition.</param>
        /// <returns>The number of partitions required.</returns>
        public static uint PartitionCount(uint itemCount, uint partitionSize)
        {
            Covenant.Requires<ArgumentException>(itemCount >= 0, nameof(itemCount));
            Covenant.Requires<ArgumentException>(partitionSize > 0, nameof(partitionSize));

            var partitions = itemCount / partitionSize;

            if (itemCount % partitionSize != 0)
            {
                partitions++;
            }

            return partitions;
        }

        /// <summary>
        /// Computes the <see cref="long"/> number of partitions that would be required to divide a
        /// set of items where the number of items in each partition is limited.
        /// </summary>
        /// <param name="itemCount">The number of items to be partitioned.</param>
        /// <param name="partitionSize">The maximim number of items in any partition.</param>
        /// <returns>The number of partitions required.</returns>
        public static long PartitionCount(long itemCount, long partitionSize)
        {
            Covenant.Requires<ArgumentException>(itemCount >= 0, nameof(itemCount));
            Covenant.Requires<ArgumentException>(partitionSize > 0, nameof(partitionSize));

            var partitions = itemCount / partitionSize;

            if (itemCount % partitionSize != 0)
            {
                partitions++;
            }

            return partitions;
        }

        /// <summary>
        /// Computes the <see cref="ulong"/> number of partitions that would be required to divide a
        /// set of items where the number of items in each partition is limited.
        /// </summary>
        /// <param name="itemCount">The number of items to be partitioned.</param>
        /// <param name="partitionSize">The maximim number of items in any partition.</param>
        /// <returns>The number of partitions required.</returns>
        public static ulong PartitionCount(ulong itemCount, ulong partitionSize)
        {
            Covenant.Requires<ArgumentException>(itemCount >= 0, nameof(itemCount));
            Covenant.Requires<ArgumentException>(partitionSize > 0, nameof(partitionSize));

            var partitions = itemCount / partitionSize;

            if (itemCount % partitionSize != 0)
            {
                partitions++;
            }

            return partitions;
        }

        /// <summary>
        /// Determines the minimum <see cref="TimeSpan"/> value.
        /// </summary>
        /// <param name="values">The values to compare.</param>
        /// <returns>The minimum of the values passed or <see cref="TimeSpan.Zero"/> when nothing is passed..</returns>
        public static TimeSpan Min(params TimeSpan[] values)
        {
            if (values == null || values.Length == 0)
            {
                return TimeSpan.Zero;
            }

            var minValue = values.First();

            foreach (var value in values.Skip(1))
            {
                if (value < minValue)
                {
                    minValue = value;
                }
            }

            return minValue;
        }

        /// <summary>
        /// Determines the maximum <see cref="TimeSpan"/> value.
        /// </summary>
        /// <param name="values">The values to compare.</param>
        /// <returns>The minimum of the values passed or <see cref="TimeSpan.Zero"/> when nothing is passed..</returns>
        public static TimeSpan Max(params TimeSpan[] values)
        {
            if (values == null || values.Length == 0)
            {
                return TimeSpan.Zero;
            }

            var maxValue = values.First();

            foreach (var value in values.Skip(1))
            {
                if (value > maxValue)
                {
                    maxValue = value;
                }
            }

            return maxValue;
        }
    }
}
