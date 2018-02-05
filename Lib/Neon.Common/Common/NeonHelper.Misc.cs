//-----------------------------------------------------------------------------
// FILE:	    NeonHelper.Misc.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Neon.Common
{
    public static partial class NeonHelper
    {
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
        /// <returns>The error string.</returns>
        public static string ExceptionError(Exception e, bool stackTrace = false)
        {
            Covenant.Requires<ArgumentNullException>(e != null);

            var aggregate = e as AggregateException;

            if (aggregate != null)
            {
                e = aggregate.InnerExceptions[0];
            }

            string message;

            if (e == null)
            {
                message = "NULL Exception";
            }
            else
            {
                message = $"[{e.GetType().Name}]: {e.Message}";
            }

            if (stackTrace && e != null)
            {
                message += $" [stack:{new StackTrace(e, skipFrames: 1, fNeedFileInfo: true)}]";
            }

            return message;
        }

        /// <summary>
        /// Starts a new <see cref="Thread"/> to perform an action.
        /// </summary>
        /// <param name="action">The action to be performed.</param>
        /// <returns>The <see cref="Thread"/>.</returns>
        public static Thread ThreadRun(Action action)
        {
            Covenant.Requires<ArgumentNullException>(action != null);

            var thread = new Thread(new ThreadStart(action));

            thread.Start();

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
            Covenant.Requires<ArgumentNullException>(action != null);

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
            Covenant.Requires<ArgumentNullException>(action != null);

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
        /// Reads a password from the <see cref="Console"/> terminated by <b>Enter</b>
        /// without echoing the typed characters.
        /// </summary>
        /// <param name="prompt">Optional prompt.</param>
        /// <returns>The password entered.</returns>
        public static string ReadConsolePassword(string prompt = null)
        {
            if (!string.IsNullOrEmpty(prompt))
            {
                Console.Write(prompt);
            }

            var password = string.Empty;

            while (true)
            {
                var key = Console.ReadKey(true);
                var ch  = key.KeyChar;

                if (ch == '\r' || ch == '\n')
                {
                    Console.WriteLine();

                    return password.Trim();
                }
                else if (ch == '\b' && password.Length > 0)
                {
                    password = password.Substring(0, password.Length - 1);
                }
                else
                {
                    password += ch;
                }
            }
        }

        /// <summary>
        /// Expands any embedded TAB <b>(\t)</b> characters in the string passed
        /// into spaces such that the tab stops will be formatted correctly.
        /// </summary>
        /// <param name="input">The input string.</param>
        /// <param name="tabStop">The tab stop width in characters (defaults to <b>4</b>).</param>
        /// <returns>The expanded string.</returns>
        /// <remarks>
        /// <note>
        /// If the string passed includes line ending characters (CR or LF) then 
        /// the output will include line endings for every line, including the
        /// last one.
        /// </note>
        /// </remarks>
        public static string ExpandTabs(string input, int tabStop = 4)
        {
            Covenant.Requires<ArgumentNullException>(input != null);
            Covenant.Requires<ArgumentException>(tabStop >= 1);

            if (tabStop == 1)
            {
                return input.Replace('\t', ' ');
            }

            var lineEndings = input.IndexOfAny(new char[] { '\r', '\n' }) >= 0;
            var sb          = new StringBuilder((int)(input.Length * 1.25));

            using (var reader = new StringReader(input))
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

            return sb.ToString();
        }

        /// <summary>
        /// Waits for a boolean function to return <c>true</c>.
        /// </summary>
        /// <param name="action">The boolean delegate.</param>
        /// <param name="timeout">The maximum time to wait.</param>
        /// <param name="pollTime">The time to wait between polling or <c>null</c> for a reasonable default.</param>
        /// <exception cref="TimeoutException">Thrown if the never returned <c>true</c> before the timeout.</exception>
        /// <remarks>
        /// This method periodically calls <paramref name="action"/> until it
        /// returns <c>true</c> or <pararef name="timeout"/> exceeded.
        /// </remarks>
        public static void WaitFor(Func<bool> action, TimeSpan timeout, TimeSpan? pollTime = null)
        {
            var timeLimit = DateTimeOffset.UtcNow + timeout;

            if (!pollTime.HasValue)
            {
                pollTime = TimeSpan.FromMilliseconds(250);
            }

            while (true)
            {
                if (action())
                {
                    return;
                }

                Task.Delay(pollTime.Value).Wait();

                if (DateTimeOffset.UtcNow >= timeLimit)
                {
                    throw new TimeoutException();
                }
            }
        }

        /// <summary>
        /// Asynchronously waits for a boolean function to return <c>true</c>.
        /// </summary>
        /// <param name="action">The boolean delegate.</param>
        /// <param name="timeout">The maximum time to wait.</param>
        /// <param name="pollTime">The time to wait between polling or <c>null</c> for a reasonable default.</param>
        /// <exception cref="TimeoutException">Thrown if the never returned <c>true</c> before the timeout.</exception>
        /// <remarks>
        /// This method periodically calls <paramref name="action"/> until it
        /// returns <c>true</c> or <pararef name="timeout"/> exceeded.
        /// </remarks>
        public static async Task WaitForAsync(Func<Task<bool>> action, TimeSpan timeout, TimeSpan? pollTime = null)
        {
            var timeLimit = DateTimeOffset.UtcNow + timeout;

            if (!pollTime.HasValue)
            {
                pollTime = TimeSpan.FromMilliseconds(250);
            }

            while (true)
            {
                if (await action())
                {
                    return;
                }

                await Task.Delay(pollTime.Value);

                if (DateTimeOffset.UtcNow >= timeLimit)
                {
                    throw new TimeoutException();
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
        public static async Task WaitAllAsync(IEnumerable<Task> tasks)
        {
            foreach (var task in tasks)
            {
                await task;
            }
        }

        /// <summary>
        /// Asynchronously waits for all of the <see cref="Task"/>s passed to complete.
        /// </summary>
        /// <param name="tasks">The tasks to wait on.</param>
        public static async Task WaitAllAsync(params Task[] tasks)
        {
            foreach (var task in tasks)
            {
                await task;
            }
        }

        /// <summary>
        /// Asynchronously waits for all of the <see cref="Task"/>s passed to complete.
        /// </summary>
        /// <param name="tasks">The tasks to wait for.</param>
        /// <param name="timeout">The optional timeout.</param>
        /// <param name="cancellationToken">The optional cancellation token.</param>
        /// <exception cref="TimeoutException">Thrown if the <paramref name="timeout"/> was exceeded.</exception>
        public static async Task WaitAllAsync(IEnumerable<Task> tasks, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            // There isn't a super clean way to implement this other than polling.

            if (!timeout.HasValue)
            {
                timeout = TimeSpan.FromDays(365); // Set an essentially infinite timeout
            }

            var stopwatch = new Stopwatch();

            stopwatch.Start();

            while (true)
            {
                var isCompleted = true;

                foreach (var task in tasks)
                {
                    if (!task.IsCompleted)
                    {
                        isCompleted = false;
                    }
                }

                if (isCompleted)
                {
                    return;
                }

                cancellationToken.ThrowIfCancellationRequested();

                if (stopwatch.Elapsed >= timeout)
                {
                    throw new TimeoutException();
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
        /// <remarks>
        /// <para>
        /// I have run into a situation in the <see cref="Neon.DynamicData.DynamicEntity"/> implementation
        /// where I've serialized a Couchbase Lite document and then when loading the updated
        /// revision, <see cref="JToken.EqualityComparer"/> indicates that two properties with
        /// the same name and <c>null</c> values are different.
        /// </para>
        /// <para>
        /// I think the basic problem is that a NULL token has a different token type than
        /// a string token with a NULL value and also that some token types such as <see cref="JTokenType.Date"/>,
        /// <see cref="JTokenType.Guid"/>, <see cref="JTokenType.TimeSpan"/>, and <see cref="JTokenType.Uri"/> 
        /// will be round tripped as <see cref="JTokenType.String"/> values.
        /// </para>
        /// <para>
        /// This method addresses these issues.
        /// </para>
        /// </remarks>
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

                    var object1       = (JObject)token1;
                    var object2       = (JObject)token2;
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

                    var array1       = (JArray)token1;
                    var array2       = (JArray)token2;
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
        /// Generates a cryptographically random password.
        /// </summary>
        /// <param name="length">The password length.</param>
        /// <returns>The generated password.</returns>
        public static string GetRandomPassword(int length)
        {
            Covenant.Requires<ArgumentException>(length > 0);

            var sb    = new StringBuilder(length);
            var chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var bytes = RandBytes(length);

            foreach (var v in bytes)
            {
                sb.Append(chars[v % chars.Length]);
            }

            return sb.ToString();
        }

        /// <summary>
        /// Used to temporarily modify the <b>hosts</b> file used by the DNS resolver
        /// for debugging purposes.
        /// </summary>
        /// <param name="hostEntries">A dictionary mapping the host names to an IP address or <c>null</c>.</param>
        /// <remarks>
        /// <note>
        /// This requires elevated administrative privileges.  You'll need to launch Visual Studio
        /// or favorite development envirnment with these.
        /// </note>
        /// <para>
        /// This method adds or removes a temporary section of host entry definitions
        /// delimited by special comment lines.  When <paramref name="hostEntries"/> is 
        /// non-null or empty, the section will be added or updated.  Otherwise, the
        /// section will be removed.
        /// </para>
        /// </remarks>
        public static void ModifyHostsFile(Dictionary<string, IPAddress> hostEntries = null)
        {
#if XAMARIN
            throw new NotSupportedException();
#else
            string hostsPath;

            if (NeonHelper.IsWindows)
            {
                hostsPath = Path.Combine(Environment.GetEnvironmentVariable("windir"), "System32", "drivers", "etc", "hosts");
            }
            else if (NeonHelper.IsLinux || NeonHelper.IsOSX)
            {
                hostsPath = "/etc/hosts";
            }
            else
            {
                throw new NotSupportedException();
            }

            const string beginMarker = "# BEGIN TEMPORARY SECTION";
            const string endMarker   = "# END TEMPORARY SECTION";

            var inputLines  = File.ReadAllLines(hostsPath);
            var lines       = new List<string>();
            var tempSection = false;

            // Strip out any existing temporary sections.

            foreach (var line in inputLines)
            {
                switch (line.Trim())
                {
                    case beginMarker:

                        tempSection = true;
                        break;

                    case endMarker:

                        tempSection = false;
                        break;

                    default:

                        if (!tempSection)
                        {
                            lines.Add(line);
                        }
                        break;
                }
            }

            if (hostEntries?.Count > 0)
            {
                // Append the new entries.

                lines.Add(beginMarker);

                foreach (var item in hostEntries)
                {
                    var address = item.Value.ToString();

                    lines.Add($"        {address}{new string(' ', 16 - address.Length)}    {item.Key}");
                }

                lines.Add(endMarker);
            }

            File.WriteAllLines(hostsPath, lines.ToArray());
#endif
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
        /// Recursively deletes a file folder, ignoring any errors.
        /// </summary>
        /// <param name="folder">The folder path.</param>
        public static void DeleteFolder(string folder)
        {
            if (Directory.Exists(folder))
            {
                try
                {
                    Directory.Delete(folder, recursive: true);
                }
                catch
                {
                    // Intentionally ignoring errors.
                }
            }
        }

        /// <summary>
        /// Recursively deletes the contents of a file folder, ignoring any errors.
        /// </summary>
        /// <param name="folder">The folder path.</param>
        public static void DeleteFolderContents(string folder)
        {
            if (Directory.Exists(folder))
            {
                try
                {
                    foreach (var file in Directory.GetFiles(folder, "*.*", SearchOption.TopDirectoryOnly))
                    {
                        try
                        {
                            File.Delete(file);
                        }
                        catch
                        {
                        }
                    }

                    foreach (var directory in Directory.GetDirectories(folder))
                    {
                        try
                        {
                            Directory.Delete(directory);
                        }
                        catch
                        {
                        }
                    }
                }
                catch
                {
                    // Intentionally ignoring errors.
                }
            }
        }

        /// <summary>
        /// Recursively copies the files within one directory to another, creating
        /// target folders as required.
        /// </summary>
        /// <param name="sourceFolder">The source folder.</param>
        /// <param name="targetFolder">The target folder.</param>
        /// <remarks>
        /// <note>
        /// This method does not currently copy empty folders.
        /// </note>
        /// </remarks>
        public static void CopyFolder(string sourceFolder, string targetFolder)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(sourceFolder));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(targetFolder));

            sourceFolder = Path.GetFullPath(sourceFolder);
            targetFolder = Path.GetFullPath(targetFolder);

            Directory.CreateDirectory(targetFolder);

            foreach (var sourceFile in Directory.GetFiles(sourceFolder, "*", SearchOption.AllDirectories))
            {
                var relativePath = sourceFile.Substring(sourceFolder.Length + 1);
                var targetFile   = Path.Combine(targetFolder, relativePath);

                Directory.CreateDirectory(Path.GetDirectoryName(targetFile));
                File.Copy(sourceFile, targetFile);
            }
        }

        /// <summary>
        /// Uses deflate to commpress a string.
        /// </summary>
        /// <param name="input">The input string or <c>null</c>.</param>
        /// <returns>The compressed bytes or <c>null</c>.</returns>
        public static byte[] CompressString(string input)
        {
            if (input == null)
            {
                return null;
            }

            using (var msEncrypted = new MemoryStream())
            {
                using (var deflater = new DeflateStream(msEncrypted, CompressionLevel.Optimal))
                {
                    deflater.Write(Encoding.UTF8.GetBytes(input));
                }

                return msEncrypted.ToArray();
            }
        }

        /// <summary>
        /// Uses deflate to decompress a string from compressed bytes.
        /// </summary>
        /// <param name="bytes">The compressed bytes or <c>null</c>.</param>
        /// <returns>The decompressed string or <c>null</c>.</returns>
        public static string DecompressString(byte[] bytes)
        {
            if (bytes == null)
            {
                return null;
            }

            using (var msDecrypted = new MemoryStream())
            {
                using (var msEncrypted = new MemoryStream(bytes))
                {
                    using (var deflater = new DeflateStream(msEncrypted, CompressionMode.Decompress))
                    {
                        deflater.CopyTo(msDecrypted);
                    }

                    return Encoding.UTF8.GetString(msDecrypted.ToArray());
                }
            }
        }

        /// <summary>
        /// Uses deflate to commpress a byte array.
        /// </summary>
        /// <param name="bytes">The input byte array or <c>null</c>.</param>
        /// <returns>The compressed bytes or <c>null</c>.</returns>
        public static byte[] CompressBytes(byte[] bytes)
        {
            if (bytes == null)
            {
                return null;
            }

            using (var msEncrypted = new MemoryStream())
            {
                using (var deflater = new DeflateStream(msEncrypted, CompressionLevel.Optimal))
                {
                    deflater.Write(bytes);
                }

                return msEncrypted.ToArray();
            }
        }

        /// <summary>
        /// Uses deflate to decompress a byte array from compressed bytes.
        /// </summary>
        /// <param name="bytes">The compressed bytes or <c>null</c>.</param>
        /// <returns>The decompressed string or <c>null</c>.</returns>
        public static byte[] DecompressBytes(byte[] bytes)
        {
            if (bytes == null)
            {
                return null;
            }

            using (var msDecrypted = new MemoryStream())
            {
                using (var msEncrypted = new MemoryStream(bytes))
                {
                    using (var deflater = new DeflateStream(msEncrypted, CompressionMode.Decompress))
                    {
                        deflater.CopyTo(msDecrypted);
                    }

                    return msDecrypted.ToArray();
                }
            }
        }

        /// <summary>
        /// Typesafe <c>enum</c> parser.
        /// </summary>
        /// <typeparam name="TEnum">The enumeration type.</typeparam>
        /// <param name="input">The input string.</param>
        /// <param name="ignoreCase">Optionally indicates that parsing should be case insensitive.</param>
        /// <returns>The parsed value.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="input"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException">Thrown if <paramref name="input"/> is not valid.</exception>
        public static TEnum ParseEnum<TEnum>(string input, bool ignoreCase = false)
        {
            return (TEnum)Enum.Parse(typeof(TEnum), input, ignoreCase);
        }

        /// <summary>
        /// Reads the <b>standard input</b> file to the end and returns the
        /// result as a string.
        /// </summary>
        /// <returns>The standard input.</returns>
        public static string ReadStandardInputText()
        {
            using (var input = Console.OpenStandardInput())
            {
                using (var reader = new StreamReader(input, detectEncodingFromByteOrderMarks: true))
                {
                    return reader.ReadToEnd();
                }
            }
        }

        /// <summary>
        /// Reads the <b>standard input</b> file to the end and returns the
        /// result as bytes.
        /// </summary>
        /// <returns>The standard input.</returns>
        public static byte[] ReadStandardInputBytes()
        {
            using (var stdin = Console.OpenStandardInput())
            {
                using (var ms = new MemoryStream())
                {
                    var buffer = new byte[8192];

                    while (true)
                    {
                        var cb = stdin.Read(buffer, 0, buffer.Length);

                        if (cb == 0)
                        {
                            break;
                        }

                        ms.Write(buffer, 0, cb);
                    }

                    return ms.ToArray();
                }
            }
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
                throw new ArgumentNullException("input");
            if (input.Length < 1)
                return String.Empty;

            string base64Str = null;
            int endPos = 0;
            char[] base64Chars = null;

            ////////////////////////////////////////////////////////
            // Step 1: Do a Base64 encoding
            base64Str = Convert.ToBase64String(input);
            if (base64Str == null)
                return null;

            ////////////////////////////////////////////////////////
            // Step 2: Find how many padding chars are present in the end
            for (endPos = base64Str.Length; endPos > 0; endPos--)
            {
                if (base64Str[endPos - 1] != '=') // Found a non-padding char!
                {
                    break; // Stop here
                }
            }

            ////////////////////////////////////////////////////////
            // Step 3: Create char array to store all non-padding chars,
            //      plus a char to indicate how many padding chars are needed
            base64Chars = new char[endPos + 1];
            base64Chars[endPos] = (char)((int)'0' + base64Str.Length - endPos); // Store a char at the end, to indicate how many padding chars are needed

            ////////////////////////////////////////////////////////
            // Step 3: Copy in the other chars. Transform the "+" to "-", and "/" to "_"
            for (int iter = 0; iter < endPos; iter++)
            {
                char c = base64Str[iter];

                switch (c)
                {
                    case '+':
                        base64Chars[iter] = '-';
                        break;

                    case '/':
                        base64Chars[iter] = '_';
                        break;

                    case '=':
                        //Debug.Assert(false);
                        base64Chars[iter] = c;
                        break;

                    default:
                        base64Chars[iter] = c;
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
                throw new ArgumentNullException("input");

            int len = input.Length;
            if (len < 1)
                return new byte[0];

            ///////////////////////////////////////////////////////////////////
            // Step 1: Calculate the number of padding chars to append to this string.
            //         The number of padding chars to append is stored in the last char of the string.
            int numPadChars = (int)input[len - 1] - (int)'0';
            if (numPadChars < 0 || numPadChars > 10)
                return null;


            ///////////////////////////////////////////////////////////////////
            // Step 2: Create array to store the chars (not including the last char)
            //          and the padding chars
            char[] base64Chars = new char[len - 1 + numPadChars];


            ////////////////////////////////////////////////////////
            // Step 3: Copy in the chars. Transform the "-" to "+", and "*" to "/"
            for (int iter = 0; iter < len - 1; iter++)
            {
                char c = (char)input[iter];

                switch (c)
                {
                    case '-':
                        base64Chars[iter] = '+';
                        break;

                    case '_':
                        base64Chars[iter] = '/';
                        break;

                    default:
                        base64Chars[iter] = c;
                        break;
                }
            }

            ////////////////////////////////////////////////////////
            // Step 4: Add padding chars
            for (int iter = len - 1; iter < base64Chars.Length; iter++)
            {
                base64Chars[iter] = '=';
            }

            // Do the actual conversion
            return Convert.FromBase64CharArray(base64Chars, 0, base64Chars.Length);
        }

        /// <summary>
        /// Returns the fully qualified path to the folder holding the
        /// assembly passed (includes the terminating "\").
        /// </summary>
        /// <param name="assembly">The assembly.</param>
        /// <returns>Path to the folder holding the assembly.</returns>
        public static string GetAssemblyFolder(Assembly assembly)
        {
            // Get the path to the directory hosting the assembly by
            // stripping off the file URI scheme if present and the
            // assembly's file name.

            string path;
            int pos;

            path = NeonHelper.StripFileScheme(assembly.CodeBase);

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
        public static string GetAssemblyPath(Assembly assembly)
        {
            // Get the path to the directory hosting the assembly by
            // stripping off the file URI scheme if present and the
            // assembly's file name.

            return NeonHelper.StripFileScheme(assembly.CodeBase);
        }
    }
}
