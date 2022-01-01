//-----------------------------------------------------------------------------
// FILE:	    XunitExtensions.cs
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

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using Neon.ModelGen;
using Neon.Common;
using Neon.Data;

namespace Neon.Xunit
{
    /// <summary>
    /// Unit test related extensions.
    /// </summary>
    public static class XunitExtensions
    {
        //---------------------------------------------------------------------
        // IGeneratedServiceClient extensions

        private class ErrorWriter
        {
            private StringBuilder sb = new StringBuilder();

            public void Error(string message)
            {
                HasErrors = true;

                if (sb.Length > 0)
                {
                    sb.AppendLine();
                }

                sb.AppendLine("ERROR: " + message);
            }

            public bool HasErrors { get; private set; }

            public override string ToString()
            {
                var errors = sb.ToString();

                // $hack(jefflill):
                //
                // Remove some common namespace prefixes to make these errors easier to read.

                errors = errors.Replace("System.Collections.Generic.", string.Empty);
                errors = errors.Replace("System.Collections.ObjectModel.", string.Empty);
                errors = errors.Replace("System.Threading.Tasks.", string.Empty);
                errors = errors.Replace("System.Threading.", string.Empty);
                errors = errors.Replace("Neon.Diagnostics.", string.Empty);
                errors = errors.Replace("Neon.Retry.", string.Empty);

                errors = errors.Replace("System.Boolean", "bool");
                errors = errors.Replace("System.Byte", "byte");
                errors = errors.Replace("System.SByte", "sbyte");
                errors = errors.Replace("System.Int16", "short");
                errors = errors.Replace("System.UInt16", "ushort");
                errors = errors.Replace("System.Int32", "int");
                errors = errors.Replace("System.UInt32", "uint");
                errors = errors.Replace("System.Int64", "long");
                errors = errors.Replace("System.UInt64", "ulong");
                errors = errors.Replace("System.Float", "float");
                errors = errors.Replace("System.Double", "double");
                errors = errors.Replace("System.Decimal", "decimal");
                errors = errors.Replace("System.String", "string");

                return errors;
            }
        }

        /// <summary>
        /// <para>
        /// Compares the service model implemented by the generated service client against
        /// the actual ASP.NET service controller implementation.  This ensures that the
        /// generated client actually matches the controller implementation.
        /// </para>
        /// <note>
        /// <b>IMPORTANT:</b> You should always include a call to this in your service unit
        /// tests to ensure that the service models used to generate the service clients 
        /// actually match the service as implemented.  It is very likely for definitions
        /// and implementations to diverge over time.
        /// </note>
        /// </summary>
        /// <typeparam name="TServiceController">The service controller implementation type.</typeparam>
        /// <param name="client">The service client implementation being tested.</param>
        /// <exception cref="IncompatibleServiceException">Thrown when the service implementaton doesn't match the generated client.</exception>
        public static void ValidateController<TServiceController>(this IGeneratedServiceClient client)
        {
            // Verify that the schema for the generated code is compatable with this
            // version of the validator.  We currently require schema=1.

            var versionFields = client.GeneratorVersion.Split(':');

            if (versionFields.Length != 2 || versionFields[1] != "1")
            {
                throw new IncompatibleServiceException($"The generated service client code is not compatible with this version of the validator.  Consider upgrading to the latest Neon packages and rebuilding your service clients.");
            }

            // The idea here is to construct two dictionaries that map method signatures
            // [MethodInfo] instances.  One dictionary will do this for [TServiceImplementation]
            // and the other one for the [client] passed.
            //
            // The method signature will be formatted like:
            //
            //      HTTPMethod["RouteTemplate"]: ReturnType (Param0Type, Param1Type,...)
            //
            // Note that we're only going to include parameters from the client that
            // are tagged by [GeneratedParam].  The remaining parameters are client
            // specific, like timeout or CancellationToken.
            //
            // We're also going to use the request route template (in brackets) rather 
            // than the method name (because the names may differ).
            //
            // We'll use these dictionaries to ensure that both the service controller
            // and the generated service client implement the same methods and then
            // we'll go back and verify the details for each matching method.
            //
            // NOTE: By default, we assume that all public non-static service controller
            // methods will be validated against the generated service client.  In rare
            // situations, it may be useful to have service controller endpoint that are
            // not covered by the generated clients.  In these situations, you may
            // tag these methods with [Neon.Common.ModelGen.NoValidation] and the those
            // methods will be ignored.

            var controllerMethods = new Dictionary<string, MethodInfo>();
            var clientMethods     = new Dictionary<string, MethodInfo>();
            var controllerType    = typeof(TServiceController);
            var clientType        = client.GetType();
            var errors            = new ErrorWriter();

            // Ensure that the service controller and client route templates match.

            var generatedClientAttribute = clientType.GetCustomAttribute<GeneratedClientAttribute>();
            var controllerRouteAttribute = controllerType.GetCustomAttribute<Microsoft.AspNetCore.Mvc.RouteAttribute>();

            if (generatedClientAttribute == null)
            {
                errors.Error($"[{clientType.Name}] must be tagged with a [GeneratedClient] attribute.");
            }

            if (controllerRouteAttribute == null)
            {
                errors.Error($"[{controllerType.Name}] must be tagged with a [Route] attribute.");
            }

            if (generatedClientAttribute != null && controllerRouteAttribute != null)
            {
                if (generatedClientAttribute.RouteTemplate != controllerRouteAttribute.Template)
                {
                    errors.Error($"[{controllerType.Name}] has [Route(\"{controllerRouteAttribute.Template}\")] which does not match [{clientType.Name}] client's [Route(\"{generatedClientAttribute.RouteTemplate}\")].");
                }
            }

            // Load the controller methods.

            foreach (var method in controllerType.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            {
                var noValidationAttribute = method.GetCustomAttribute<NoControllerValidationAttribute>();

                if (noValidationAttribute != null)
                {
                    continue;
                }

                if (method.IsSpecialName)
                {
                    // Ignore property setters, getters, etc.

                    continue;
                }

                if (method.DeclaringType != controllerType && method.GetCustomAttribute<ControllerValidationAttribute>() == null)
                {
                    // Any service controller methods that are not implemented directly in the 
                    // controller class or are not tagged by [ControllerValidation] are not
                    // considered for validation.  This filters out base object methods
                    // like ToString(), GetType(),...

                    continue;
                }

                if (method.ReturnType == typeof(Microsoft.AspNetCore.Mvc.IActionResult))
                {
                    errors.Error($"Controller method [{method.Name}(...)] returns [IActionResult] which is not supported.  Use [ActionResult] or [ActionResult<T>] instead.");
                }

                var     routeAttribute = method.GetCustomAttribute<Microsoft.AspNetCore.Mvc.RouteAttribute>();
                string  routeTemplate;

                if (routeAttribute != null)
                {
                    routeTemplate = routeAttribute.Template;
                }
                else
                {
                    routeTemplate = method.Name;
                }

                var httpMethod = "GET";
                var methodName = method.Name.ToUpperInvariant();

                if (method.GetCustomAttribute<Microsoft.AspNetCore.Mvc.HttpDeleteAttribute>() != null)
                {
                    httpMethod = "DELETE";
                }
                else if (method.GetCustomAttribute<Microsoft.AspNetCore.Mvc.HttpGetAttribute>() != null)
                {
                    httpMethod = "GET";
                }
                else if (method.GetCustomAttribute<Microsoft.AspNetCore.Mvc.HttpHeadAttribute>() != null)
                {
                    httpMethod = "HEAD";
                }
                else if (method.GetCustomAttribute<Microsoft.AspNetCore.Mvc.HttpOptionsAttribute>() != null)
                {
                    httpMethod = "OPTIONS";
                }
                else if (method.GetCustomAttribute<Microsoft.AspNetCore.Mvc.HttpPatchAttribute>() != null)
                {
                    httpMethod = "PATCH";
                }
                else if (method.GetCustomAttribute<Microsoft.AspNetCore.Mvc.HttpPostAttribute>() != null)
                {
                    httpMethod = "POST";
                }
                else if (method.GetCustomAttribute<Microsoft.AspNetCore.Mvc.HttpPutAttribute>() != null)
                {
                    httpMethod = "PUT";
                }
                else if (methodName.StartsWith("DELETE"))
                {
                    httpMethod = "DELETE";
                }
                else if (methodName.StartsWith("GET"))
                {
                    httpMethod = "GET";
                }
                else if (methodName.StartsWith("HEAD"))
                {
                    httpMethod = "HEAD";
                }
                else if (methodName.StartsWith("OPTIONS"))
                {
                    httpMethod = "OPTIONS";
                }
                else if (methodName.StartsWith("PATCH"))
                {
                    httpMethod = "PATCH";
                }
                else if (methodName.StartsWith("POST"))
                {
                    httpMethod = "POST";
                }
                else if (methodName.StartsWith("PUT"))
                {
                    httpMethod = "PUT";
                }

                var signature = GetMethodSignature(method, httpMethod, routeTemplate, requireGeneratedParamAttribute: false);

                if (controllerMethods.TryGetValue(signature, out var existing))
                {
                    errors.Error($"[{controllerType.Name}] has multiple methods including\r\n\r\n    {method.Name}(...)] and\r\n\r\n    [{existing.Name}(...)]\r\n\r\n    with the same parameters at endpoint [{httpMethod}:{routeTemplate}].");
                }
                else
                {
                    controllerMethods.Add(signature, method);
                }
            }

            // Load the client methods that call service endpoints.

            foreach (var method in clientType.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            {
                var generatedMethodAttribute = method.GetCustomAttribute<GeneratedMethodAttribute>();

                if (generatedMethodAttribute == null)
                {
                    continue;
                }

                var signature = GetMethodSignature(method, generatedMethodAttribute.HttpMethod, generatedMethodAttribute.RouteTemplate, requireGeneratedParamAttribute: true);

                if (clientMethods.TryGetValue(signature, out var existing))
                {
                    errors.Error($"[{clientType.Name}] has multiple methods including\r\n\r\n    [{method.Name}(...)] and\r\n\t\n    [{existing.Name}(...)]\r\n\r\n    with the same parameters at endpoint [{generatedMethodAttribute.HttpMethod}:{generatedMethodAttribute.RouteTemplate}].");
                }
                else
                {
                    clientMethods.Add(signature, method);
                }
            }

            // Ensure that all of the client methods are also present in the controller.

            foreach (var clientMethod in clientMethods)
            {
                if (!controllerMethods.ContainsKey(clientMethod.Key))
                {
                    errors.Error($"Service controller [{controllerType.Name}] lacks a method that corresponds to:\r\n\r\n    {clientType.Name}::{clientMethod.Value}\r\n\r\n    with signature:\r\n\r\n    {clientMethod.Key}]");
                }
            }

            // Ensure that all of the controller methods are also present in the client.

            foreach (var controllerMethod in controllerMethods)
            {
                if (!clientMethods.ContainsKey(controllerMethod.Key))
                {
                    errors.Error($"Service client [{clientType.Name}] lacks a method that corresponds to:\r\n\r\n    {controllerType.Name}::{controllerMethod.Value}\r\n\r\n    with signature:\r\n\r\n    {controllerMethod.Key}");
                }
            }

            // Do a detailed comparision of the method return types and parameters.

            foreach (var clientMethod in clientMethods)
            {
                if (controllerMethods.TryGetValue(clientMethod.Key, out var controllerMethod))
                {
                    var clientParams     = clientMethod.Value.GetParameters();
                    var controllerParams = controllerMethod.GetParameters();

                    // Note that we're using the controller parameter count rather than
                    // the client parameter count because the client methods were generated
                    // with extra (optional) parameters to specify timeout, retry policy, etc.

                    for (int i = 0; i < controllerParams.Length; i++)
                    {
                        var clientParam          = clientParams[i];
                        var controllerParam      = controllerParams[i];
                        var generatedClientParam = clientParam.GetCustomAttribute<GeneratedParamAttribute>();

                        if (generatedClientParam == null)
                        {
                            // Only the leading client method parameters that are actually passed to the
                            // service have this attribute, so we can ignore parameters after these.

                            break;
                        }

                        // Ensure that parameter optionality is consistent.

                        if (clientParam.IsOptional != controllerParam.IsOptional)
                        {
                            if (controllerParam.IsOptional && !clientParam.IsOptional)
                            {
                                errors.Error($"[{clientType.Name}::{clientMethod}(...)] parameter [{clientParam.Name}] is optional which conflicts with the service definition which is not optional.");
                            }
                            else
                            {
                                errors.Error($"[{clientType.Name}::{clientMethod}(...)] parameter [{clientParam.Name}] is not optional which conflicts with the service definition which is optional.");
                            }
                        }

                        if (clientParam.IsOptional)
                        {
                            var defaultsAreEqual = true;

                            if ((clientParam.DefaultValue == null) != (controllerParam.DefaultValue == null))
                            {
                                defaultsAreEqual = false;
                            }
                            else if (clientParam.DefaultValue != null)
                            {
                                defaultsAreEqual = clientParam.DefaultValue.Equals(controllerParam.DefaultValue);
                            }

                            if (!defaultsAreEqual)
                            {
                                errors.Error($"[{clientType.Name}::{clientMethod}(...)] parameter [{clientParam.Name}] default value [{clientParam.DefaultValue}] does not match the controller default value [{controllerParam.DefaultValue}].");
                            }
                        }

                        // Ensure that the method for transmitting the parameter is consistent.

                        var fromQueryAttribute = controllerParam.GetCustomAttribute<Microsoft.AspNetCore.Mvc.FromQueryAttribute>();

                        if (fromQueryAttribute != null)
                        {
                            if (string.IsNullOrEmpty(fromQueryAttribute.Name))
                            {
                                fromQueryAttribute.Name = controllerParam.Name;
                            }

                            if (generatedClientParam.PassAs != PassAs.Query)
                            {
                                errors.Error($"[{clientType.Name}::{clientMethod}(...)] parameter [{clientParam.Name}] is passed as [From{generatedClientParam.PassAs}] which doesn't match the controller method parameter which requires [FromQuery].");
                            }
                            else if (generatedClientParam.Name != fromQueryAttribute.Name)
                            {
                                errors.Error($"[{clientType.Name}::{clientMethod}(...)] parameter [{clientParam.Name}] is passed using name [{generatedClientParam.Name}] which doesn't match the controller method parameter which requires [{fromQueryAttribute.Name}].");
                            }
                        }
                        else
                        {
                            var fromRouteAttribute = controllerParam.GetCustomAttribute<Microsoft.AspNetCore.Mvc.FromRouteAttribute>();

                            if (fromRouteAttribute != null)
                            {
                                if (generatedClientParam.PassAs != PassAs.Route)
                                {
                                    errors.Error($"[{clientType.Name}::{clientMethod}(...)] parameter [{clientParam.Name}] is passed as [From{generatedClientParam.PassAs}] which doesn't match the controller method parameter which requires [FromRoute].");
                                }
                                else if (generatedClientParam.Name != fromRouteAttribute.Name)
                                {
                                    errors.Error($"[{clientType.Name}::{clientMethod}(...)] parameter [{clientParam.Name}] is passed using route name [{generatedClientParam.Name}] which doesn't match the controller method parameter which requires [{fromRouteAttribute.Name}].");
                                }
                            }
                            else
                            {
                                var fromHeaderAttribute = controllerParam.GetCustomAttribute<Microsoft.AspNetCore.Mvc.FromHeaderAttribute>();

                                if (fromHeaderAttribute != null)
                                {
                                    if (generatedClientParam.PassAs != PassAs.Header)
                                    {
                                        errors.Error($"[{clientType.Name}::{clientMethod}(...)] parameter [{clientParam.Name}] is passed as [From{generatedClientParam.PassAs}] which doesn't match the controller method parameter which requires [FromHeader].");
                                    }
                                    else if (generatedClientParam.Name != fromHeaderAttribute.Name)
                                    {
                                        errors.Error($"[{clientType.Name}::{clientMethod}(...)] parameter [{clientParam.Name}] is passed using HTTP header [{generatedClientParam.Name}] which doesn't match the controller method parameter which requires [{fromHeaderAttribute.Name}].");
                                    }
                                }
                                else
                                {
                                    var fromBodyAttribute = controllerParam.GetCustomAttribute<Microsoft.AspNetCore.Mvc.FromBodyAttribute>();

                                    if (fromBodyAttribute != null)
                                    {
                                        if (generatedClientParam.PassAs != PassAs.Body)
                                        {
                                            errors.Error($"[{clientType.Name}::{clientMethod}(...)] parameter [{clientParam.Name}] is passed as [From{generatedClientParam.PassAs}] which doesn't match the controller method parameter which requires [FromBody].");
                                        }
                                    }
                                    else
                                    {
                                        // We default to [FromQuery] when nothing else is specified.

                                        if (generatedClientParam.PassAs != PassAs.Query)
                                        {
                                            errors.Error($"[{clientType.Name}::{clientMethod}(...)] parameter [{clientParam.Name}] is passed as [From{generatedClientParam.PassAs}] which doesn't match the controller method parameter which implicitly specifies [FromQuery].");
                                        }
                                        else if (generatedClientParam.Name != controllerParam.Name)
                                        {
                                            errors.Error($"[{clientType.Name}::{clientMethod}(...)] parameter [{clientParam.Name}] is passed using HTTP query parameter [{generatedClientParam.Name}] which doesn't match the controller method parameter which requires [{controllerParam.Name}].");
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            if (errors.HasErrors)
            {
                throw new IncompatibleServiceException("\r\n\r\n" + errors.ToString());
            }
        }

        /// <summary>
        /// Generates a method signature for a <see cref="MethodInfo"/> and route template.
        /// </summary>
        /// <param name="method">The method information.</param>
        /// <param name="httpMethod">The HTTP method required for the method call.</param>
        /// <param name="routeTemplate">The route template.</param>
        /// <param name="requireGeneratedParamAttribute">
        /// Indicates that only parameters tagged with <c>[GeneratedParam]</c> will be
        /// included in the signature.
        /// </param>
        /// <returns>The method signature.</returns>
        private static string GetMethodSignature(MethodInfo method, string httpMethod, string routeTemplate, bool requireGeneratedParamAttribute)
        {
            var sbSignature = new StringBuilder();
            var returnType  = method.ReturnType;

            // Convert [Task] and [ActionResult] return types into [void] and
            // [Task<T>] and [ActionResult<T>] return types into just [T].

            if (!returnType.IsGenericType)
            {
                if (returnType == typeof(Task) || returnType == typeof(Microsoft.AspNetCore.Mvc.ActionResult))
                {
                    returnType = typeof(void);
                }
            }
            else
            {
                if (returnType.FullName.StartsWith("System.Threading.Tasks.Task`"))
                {
                    returnType = returnType.GenericTypeArguments.First();
                }
                else if (returnType.FullName.StartsWith("Microsoft.AspNetCore.Mvc.ActionResult`"))
                {
                    returnType = returnType.GenericTypeArguments.First();
                }
            }

            sbSignature.Append($"{httpMethod}[\"{routeTemplate}\"]: {NormalizeType(returnType)} (");

            var firstParam = true;

            foreach (var parameter in method.GetParameters())
            {
                if (requireGeneratedParamAttribute)
                {
                    var generatedParamAttribute = parameter.GetCustomAttribute<GeneratedParamAttribute>();

                    if (generatedParamAttribute == null)
                    {
                        continue;
                    }
                }

                if (firstParam)
                {
                    firstParam = false;
                }
                else
                {
                    sbSignature.Append(", ");
                }

                sbSignature.Append(NormalizeType(parameter.ParameterType));
            }

            sbSignature.Append(")");

            return sbSignature.ToString();
        }

        /// <summary>
        /// Normalizes types to strings including converting <see cref="ObservableCollection{T}"/> 
        /// types to <see cref="List{T}"/>.
        /// </summary>
        /// <param name="type">The input type.</param>
        /// <returns>The normalized type rendered as a string.</returns>
        private static string NormalizeType(Type type)
        {
            var renderedType = type.ToString();

            if (!type.IsGenericType)
            {
                return renderedType;
            }

            const string pattern = "System.Collections.ObjectModel.ObservableCollection`";

            if (renderedType.StartsWith(pattern))
            {
                return "System.Collections.Generic.List`" + renderedType.Substring(pattern.Length);
            }
            else
            {
                return renderedType;
            }
        }
    }
}
