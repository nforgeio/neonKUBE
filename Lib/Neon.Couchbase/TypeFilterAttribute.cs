//-----------------------------------------------------------------------------
// FILE:	    TypeFilterAttribute.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.
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

using System;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Linq.Expressions;

using Couchbase;
using Couchbase.Authentication;
using Couchbase.Configuration.Client;
using Couchbase.Core;
using Couchbase.IO;
using Couchbase.Linq.Filters;

using Neon.Common;
using Neon.Data;
using Neon.Retry;

namespace Couchbase
{
    /// <summary>
    /// Use this to decorate an <see cref="Entity{T}"/> implementations such that Linq2Couchbase
    /// queries will automatically add where clause that filters on a specified document
    /// type string.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Use this to decorate an <see cref="Entity{T}"/> implementations such that Linq2Couchbase
    /// queries will automatically add where clause that filters on the specified document
    /// type string.  This works much the same as the standard <see cref="DocumentTypeFilterAttribute"/>
    /// except that this implementation defaults to query on the <b>Type</b> property (with a capital "T")
    /// and the standard filter queries on <b>type</b> (all lowercase).
    /// </para>
    /// <para>
    /// You can customize the property name by setting <see cref="JsonProperty"/>.
    /// </para>
    /// </remarks>
    public class TypeFilterAttribute : DocumentFilterAttribute
    {
        /// <summary>
        /// Filter the results to include documents with this string as the "type" attribute
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// The entity's JSON property name to query.  This defaults to <b>"__EntityType"</b>.
        /// </summary>
        public string JsonProperty { get; set; } = "__EntityType";

        /// <summary>
        /// Creates a new DocumentTypeFilterAttribute
        /// </summary>
        /// <param name="type">Filter the results to include documents with this string as the <b>Type</b> attribute</param>
        public TypeFilterAttribute(string type)
        {
            Type = type;
        }

        /// <summary>
        /// Apply the filter to a LINQ query
        /// </summary>
        public override IDocumentFilter<T> GetFilter<T>()
        {
            return new WhereFilter<T>
            {
                Priority        = Priority,
                WhereExpression = GetExpression<T>()
            };
        }

        private Expression<Func<T, bool>> GetExpression<T>()
        {
            var parameter = Expression.Parameter(typeof(T), "p");

            return Expression.Lambda<Func<T, bool>>(Expression.Equal(Expression.PropertyOrField(parameter, JsonProperty), Expression.Constant(Type)), parameter);
        }

        private class WhereFilter<T> : IDocumentFilter<T>
        {
            public Expression<Func<T, bool>> WhereExpression { get; set; }
            public int Priority { get; set; }

            public IQueryable<T> ApplyFilter(IQueryable<T> source)
            {
                return source.Where(WhereExpression);
            }
        }
    }
}
