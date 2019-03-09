//-----------------------------------------------------------------------------
// FILE:	    MvcAttributes.cs
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
// limitations under the License.

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Text;

namespace Neon.CodeGen
{
    /// <summary>
    /// Used to indicate that a <c>interface</c> should be used to
    /// processed as a service API
    /// </summary>
    [AttributeUsage(AttributeTargets.Interface)]
    public class ServiceAttribute : Attribute
    {
    }

    /// <summary>
    /// Used to customize how generated client methods are grouped.
    /// </summary>
    [AttributeUsage(AttributeTargets.Interface)]
    public class ClientGroupAttribute : Attribute
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="name">The optional group name.</param>
        public ClientGroupAttribute(string name = null)
        {
            this.Name = name;
        }

        /// <summary>
        /// The group name (or <c>null</c>).
        /// </summary>
        public string Name { get; set; }
    }

    /// <summary>
    /// Used to customize request routing.
    /// </summary>
    [AttributeUsage(AttributeTargets.Interface | AttributeTargets.Method)]
    public class RouteAttribute : Attribute
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="template">The optional routing template.</param>
        public RouteAttribute(string template = null)
        {
            this.Template = template;
        }

        /// <summary>
        /// The route template.
        /// </summary>
        public string Template { get; set; }

        /// <summary>
        /// The route name.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// <b>NOT SUPPORTED:</b> The order in which the route is to be applied.
        /// </summary>
        public int Order
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }
    }

    /// <summary>
    /// Used to identify a service endpoint that is triggered via the <b>GET</b> method.
    /// </summary>
    [AttributeUsage(AttributeTargets.Interface)]
    public class HttpGetAttribute : Attribute
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="template">The optional routing template.</param>
        public HttpGetAttribute(string template = null)
        {
            this.Template = template;
            this.HttpMethod = "GET";
        }

        /// <summary>
        /// The route template.
        /// </summary>
        public string Template { get; set; }

        /// <summary>
        /// Returns the HTTP method.
        /// </summary>
        public string HttpMethod { get; private set; }

        /// <summary>
        /// <b>NOT SUPPORTED:</b> The order in which the route is to be applied.
        /// </summary>
        public int Order
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }
    }

    /// <summary>
    /// Used to identify a service endpoint that is triggered via the <b>POST</b> method.
    /// </summary>
    [AttributeUsage(AttributeTargets.Interface)]
    public class HttpPostAttribute : Attribute
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="template">The optional routing template.</param>
        public HttpPostAttribute(string template = null)
        {
            this.Template = template;
            this.HttpMethod = "POST";
        }

        /// <summary>
        /// The route template.
        /// </summary>
        public string Template { get; set; }

        /// <summary>
        /// Optionally overrides the tagged service endpoint method name when
        /// generating the client code.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Returns the HTTP method.
        /// </summary>
        public string HttpMethod { get; private set; }

        /// <summary>
        /// <b>NOT SUPPORTED:</b> The order in which the route is to be applied.
        /// </summary>
        public int Order
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }
    }

    /// <summary>
    /// Used to identify a service endpoint that is triggered via the <b>PUT</b> method.
    /// </summary>
    [AttributeUsage(AttributeTargets.Interface)]
    public class HttpPutAttribute : Attribute
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="template">The optional routing template.</param>
        public HttpPutAttribute(string template = null)
        {
            this.Template = template;
            this.HttpMethod = "PUT";
        }

        /// <summary>
        /// The route template.
        /// </summary>
        public string Template { get; set; }

        /// <summary>
        /// Optionally overrides the tagged service endpoint method name when
        /// generating the client code.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Returns the HTTP method.
        /// </summary>
        public string HttpMethod { get; private set; }

        /// <summary>
        /// <b>NOT SUPPORTED:</b> The order in which the route is to be applied.
        /// </summary>
        public int Order
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }
    }

    /// <summary>
    /// Used to identify a service endpoint that is triggered via the <b>PATCH</b> method.
    /// </summary>
    [AttributeUsage(AttributeTargets.Interface)]
    public class HttpPatchAttribute : Attribute
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="template">The optional routing template.</param>
        public HttpPatchAttribute(string template = null)
        {
            this.Template = template;
            this.HttpMethod = "PATCH";
        }

        /// <summary>
        /// The route template.
        /// </summary>
        public string Template { get; set; }

        /// <summary>
        /// Optionally overrides the tagged service endpoint method name when
        /// generating the client code.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Returns the HTTP method.
        /// </summary>
        public string HttpMethod { get; private set; }

        /// <summary>
        /// <b>NOT SUPPORTED:</b> The order in which the route is to be applied.
        /// </summary>
        public int Order
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }
    }

    /// <summary>
    /// Used to identify a service endpoint that is triggered via the <b>HEAD</b> method.
    /// </summary>
    [AttributeUsage(AttributeTargets.Interface)]
    public class HttpHeadAttribute : Attribute
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="template">The optional routing template.</param>
        public HttpHeadAttribute(string template = null)
        {
            this.Template = template;
            this.HttpMethod = "HEAD";
        }

        /// <summary>
        /// The route template.
        /// </summary>
        public string Template { get; set; }

        /// <summary>
        /// Optionally overrides the tagged service endpoint method name when
        /// generating the client code.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Returns the HTTP method.
        /// </summary>
        public string HttpMethod { get; private set; }

        /// <summary>
        /// <b>NOT SUPPORTED:</b> The order in which the route is to be applied.
        /// </summary>
        public int Order
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }
    }

    /// <summary>
    /// Used to identify a service endpoint that is triggered via the <b>OPTIONS</b> method.
    /// </summary>
    [AttributeUsage(AttributeTargets.Interface)]
    public class HttpOptionsAttribute : Attribute
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="template">The optional routing template.</param>
        public HttpOptionsAttribute(string template = null)
        {
            this.Template = template;
            this.HttpMethod = "OPTIONS";
        }

        /// <summary>
        /// The route template.
        /// </summary>
        public string Template { get; set; }

        /// <summary>
        /// Optionally overrides the tagged service endpoint method name when
        /// generating the client code.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Returns the HTTP method.
        /// </summary>
        public string HttpMethod { get; private set; }

        /// <summary>
        /// <b>NOT SUPPORTED:</b> The order in which the route is to be applied.
        /// </summary>
        public int Order
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }
    }

    /// <summary>
    /// Used to indicate that a service endpoint parameter is to be obtained
    /// by parsing the request body as JSON.
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter)]
    public class FromBodyAttribute : Attribute
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        public FromBodyAttribute()
        {
        }

        /// <summary>
        /// This is ignored.
        /// </summary>
        public string Name { get; set; }
    }

    /// <summary>
    /// Used to indicate that a service endpoint parameter is to be obtained
    /// by parsing a request header value.
    /// </summary>
    /// <remarks>
    /// By default, this option will look for the HTTP header with the same
    /// name as the tagged endpoint parameter.  This can be overriden by setting
    /// the <see cref="Name"/> property.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Parameter)]
    public class FromHeaderAttribute : Attribute
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="name">The source header name.</param>
        public FromHeaderAttribute(string name)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name));

            this.Name = name;
        }

        /// <summary>
        /// Optionally overrides the tagged service endpoint method property
        /// name when generating the client code.
        /// </summary>
        public string Name { get; set; }
    }

    /// <summary>
    /// Used to indicate that a service endpoint parameter is to be obtained
    /// by parsing a request URI query parameter.
    /// </summary>
    /// <remarks>
    /// By default, this option will look for the query parameter with the same
    /// name as the tagged endpoint parameter.  This can be overriden by setting
    /// the <see cref="Name"/> property.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Parameter)]
    public class FromQueryAttribute : Attribute
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="name">The source query parameter name.</param>
        public FromQueryAttribute(string name)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name));

            this.Name = name;
        }

        /// <summary>
        /// Optionally overrides the tagged service endpoint method property
        /// name when generating the client code.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Used to indicate that a service endpoint parameter is to be obtained
        /// by extracting the value from thr route template.
        /// </summary>
        /// <remarks>
        /// By default, this option will look for the template name parameter with the same
        /// name as the tagged endpoint parameter.  This can be overriden by setting
        /// the <see cref="Name"/> property.
        /// </remarks>
        [AttributeUsage(AttributeTargets.Parameter)]
        public class FromRouteAttribute : Attribute
        {
            /// <summary>
            /// Constructor.
            /// </summary>
            /// <param name="name">The source query parameter name.</param>
            public FromRouteAttribute(string name)
            {
                Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name));

                this.Name = name;
            }

            /// <summary>
            /// Optionally overrides the tagged service endpoint method property
            /// name when generating the client code.
            /// </summary>
            public string Name { get; set; }
        }
    }
}