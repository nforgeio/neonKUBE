//-----------------------------------------------------------------------------
// FILE:        Covenant.cs
// CONTRIBUTOR:	Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2021 by neonFORGE LLC.  All rights reserved.

using System;
using System.Diagnostics;
using System.Reflection;
using System.Text;

using Neon.Diagnostics;

// $todo(jefflill):
//
// This code is currently supporting only the documentation of Contract requirements
// but doesn't actually enforce anything.

namespace System.Diagnostics.Contracts
{
    /// <summary>
    /// A simple, lightweight, and partial implementation of the Microsoft Dev Labs <c>Contract</c> class.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class is intended to be a drop-in replacement for code contract assertions by simply
    /// searching and replacing <b>"Contract."</b> with "<see cref="Covenant"/>." in all source code.
    /// In my experience, code contracts slow down build times too much and often obsfucate 
    /// <c>async</c> methods such that they cannot be debugged effectively using the debugger.
    /// Code Contracts are also somewhat of a pain to configure as project propoerties.
    /// </para>
    /// <para>
    /// This class includes the <see cref="Requires(bool, string)"/>, <see cref="Requires{TException}(bool, string, string)"/>
    /// and <see cref="Assert(bool, string)"/> methods that can be used to capture validation
    /// requirements in code, but these methods don't currently generate any code. 
    /// </para>
    /// </remarks>
    public static class Covenant
    {
        private static Type[]   oneStringArg  = new Type[] { typeof(string) };
        private static Type[]   twoStringArgs = new Type[] { typeof(string), typeof(string) };

        /// <summary>
        /// Verifies a method pre-condition.
        /// </summary>
        /// <param name="condition">The condition to be tested.</param>
        /// <param name="message">An optional message to be included in the exception thrown.</param>
        /// <exception cref="AssertException">Thrown if <paramref name="condition"/> is <c>false</c>.</exception>
        public static void Requires(bool condition, string message = null)
        {
            if (!condition)
            {
                throw new AssertException(message);
            }
        }

        /// <summary>
        /// Verifies a method pre-condition throwing a custom exception.
        /// </summary>
        /// <typeparam name="TException">The exception to be thrown if the condition is <c>false</c>.</typeparam>
        /// <param name="condition">The condition to be tested.</param>
        /// <param name="arg1">The first optional string argument to the exception constructor.</param>
        /// <param name="arg2">The second optional string argument to the exception constructor.</param>
        /// <remarks>
        /// <para>
        /// This method throws a <typeparamref name="TException"/> instance when <paramref name="condition"/>
        /// is <c>false</c>.  Up to two string arguments may be passed to the exception constructor when an
        /// appropriate constructor exists, otherwise these arguments will be ignored.
        /// </para>
        /// </remarks>
        public static void Requires<TException>(bool condition, string arg1 = null, string arg2 = null)
            where TException : Exception, new()
        {
            if (condition)
            {
                return;
            }

            var exceptionType = typeof(TException);

            // Look for a constructor with two string parameters.

            var constructor = exceptionType.GetConstructor(twoStringArgs);

            if (constructor != null)
            {
                throw (Exception)constructor.Invoke(new object[] { arg1, arg2 });
            }

            // Look for a constructor with one string parameter.

            constructor = exceptionType.GetConstructor(oneStringArg);

            if (constructor != null)
            {
                throw (Exception)constructor.Invoke(new object[] { arg1 });
            }

            // Fall back to the default constructor.

            throw new TException();
        }

        /// <summary>
        /// Asserts that a condition is <c>true</c>.
        /// </summary>
        /// <param name="condition">The condition to be tested.</param>
        /// <param name="message">An optional message to be included in the exception thrown.</param>
        /// <exception cref="AssertException">Thrown if <paramref name="condition"/> is <c>false</c>.</exception>
        public static void Assert(bool condition, string message = null)
        {
            if (!condition)
            {
                throw new AssertException(message);
            }
        }
    }
}
