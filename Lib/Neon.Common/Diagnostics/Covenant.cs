//-----------------------------------------------------------------------------
// FILE:        Covenant.cs
// CONTRIBUTOR:	Jeff Lill
// COPYRIGHT:   Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Diagnostics;
using System.Reflection;
using System.Text;

using Neon.Diagnostics;

// $todo(jeff.lill):
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
    /// This class includes the <see cref="Requires(bool, string)"/>, <see cref="Requires{TException}(bool, string)"/>
    /// and <see cref="Assert(bool, string)"/> methods that can be used to capture validation
    /// requirements in code, but these methods don't currently generate any code. 
    /// </para>
    /// </remarks>
    public static class Covenant
    {
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
        /// <param name="message">An optional message to be included in the exception thrown.</param>
        public static void Requires<TException>(bool condition, string message = null)
            where TException : Exception, new()
        {
            // $todo(jeff.lill): 
            //
            // I'm currently ignoring the [message].  For some environments, it
            // could be possible to dynamically construct the exception, passing
            // the message.

            if (!condition)
            {
                throw new TException();
            }
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
