//-----------------------------------------------------------------------------
// FILE:	    ExceptionExtensions.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Diagnostics.Contracts;

namespace System
{
    /// <summary>
    /// <see cref="Exception"/> extensions.
    /// </summary>
    public static class ExceptionExtensions
    {
        /// <summary>
        /// Determines whether the exception has a specified type or was triggered by
        /// an underlying exception of a specified type.  This is useful for checking whether
        /// an exception has a specific inner exception or whether an <see cref="AggregateException"/>
        /// was triggered with a specific exception type.
        /// </summary>
        /// <typeparam name="T">The target exception type.</typeparam>
        /// <param name="e">The exception being tested.</param>
        /// <returns><c>true</c> if the exception was triggered by an exception of type <typeparamref name="T"/>.</returns>
        public static bool Contains<T>(this Exception e)
            where T : Exception
        {
            return Find<T>(e) != null;
        }

        /// <summary>
        /// Searches an exception for an underlying exception of a specific type specified as a generic
        /// type parameter.   This is useful for  checking whether an exception has a specific inner 
        /// exception or whether an <see cref="AggregateException"/> was triggered with a specific 
        /// exception type.
        /// </summary>
        /// <typeparam name="T">The target exception type.</typeparam>
        /// <param name="e">The exception being tested.</param>
        /// <returns>The underlying exception or <c>null</c>.</returns>
        public static T Find<T>(this Exception e)
            where T : Exception
        {
            if (e == null)
            {
                return null;
            }

            var value = e as T;

            if (value != null)
            {
                return value;
            }

            var aggregate = e as AggregateException;

            if (aggregate != null)
            {
                foreach (var inner in aggregate.InnerExceptions)
                {
                    if (inner != null)
                    {
                        var found = inner.Find<T>();

                        if (found != null)
                        {
                            return found;
                        }
                    }
                }

                return null;
            }
            else
            {
                if (e.InnerException != null)
                {
                    return e.InnerException.Find<T>();
                }
                else
                {
                    return null;
                }
            }
        }

        /// <summary>
        /// Searches an exception for an underlying exception of a specific type.  This is useful for 
        /// checking whether an exception has a specific inner exception or whether an <see cref="AggregateException"/>
        /// was triggered with a specific exception type.
        /// </summary>
        /// <param name="e">The exception being tested.</param>
        /// <param name="exceptionType">The target exception type.</param>
        /// <returns>The underlying exception or <c>null</c>.</returns>
        public static Exception Find(this Exception e, Type exceptionType)
        {
            Covenant.Requires<ArgumentNullException>(exceptionType != null);

            if (e == null)
            {
                return null;
            }

            if (e.GetType() == exceptionType)
            {
                return e;
            }

            var aggregate = e as AggregateException;

            if (aggregate != null)
            {
                foreach (var inner in aggregate.InnerExceptions)
                {
                    if (inner != null)
                    {
                        var found = inner.Find(exceptionType);

                        if (found != null)
                        {
                            return found;
                        }
                    }
                }

                return null;
            }
            else
            {
                if (e.InnerException != null)
                {
                    return e.InnerException.Find(exceptionType);
                }
                else
                {
                    return null;
                }
            }
        }
    }
}
