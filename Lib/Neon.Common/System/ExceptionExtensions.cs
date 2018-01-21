//-----------------------------------------------------------------------------
// FILE:	    ExceptionExtensions.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;

namespace System
{
    /// <summary>
    /// <see cref="Exception"/> extensions.
    /// </summary>
    public static class ExceptionExtensions
    {
        /// <summary>
        /// Determines whether the exception has a specified type or was triggered by
        /// an underlying exception of the type.  This is useful for checking whether
        /// an exception has a specific inner exception or whether an <see cref="AggregateException"/>
        /// was triggered with a specific exception type.
        /// </summary>
        /// <typeparam name="T">The target exception type.</typeparam>
        /// <param name="e">The exception being tested.</param>
        /// <returns><c>true</c> if the exception was triggered by an exception of type <typeparamref name="T"/>.</returns>
        public static bool TriggeredBy<T>(this Exception e)
            where T : Exception
        {
            return GetTrigger<T>(e) != null;
        }

        /// <summary>
        /// Searches an exception to for an underlying exception of a specific type.  This is useful for 
        /// checking whether an exception has a specific inner exception or whether an <see cref="AggregateException"/>
        /// was triggered with a specific exception type.
        /// </summary>
        /// <typeparam name="T">The target exception type.</typeparam>
        /// <param name="e">The exception being tested.</param>
        /// <returns>The underlying exception or <c>null</c>.</returns>
        public static T GetTrigger<T>(this Exception e)
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
                        var found = inner.GetTrigger<T>();

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
                    return e.InnerException.GetTrigger<T>();
                }
                else
                {
                    return null;
                }
            }
        }
    }
}
