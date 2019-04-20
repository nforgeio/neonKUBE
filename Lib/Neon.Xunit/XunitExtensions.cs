//-----------------------------------------------------------------------------
// FILE:	    XunitExtensions.cs
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
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using Neon.CodeGen;
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
            // The idea here is to construct two dictionaries that map method signatures
            // [MethodInfo] instances.  One dictionary will do this for [TServiceImplementation]
            // and the other one for the [client] passed.
            //
            // The method signature will be formatted like:
            //
            //      ReturnType [HTTPMethod]:[RouteTemplate](Param0Type, Param1Type,...)
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
            // tag these methods with [Neon.Common.CodeGen.NoValidation] and the those
            // methods will be ignored.

            var controllerMethods = new Dictionary<string, MethodInfo>();
            var clientMethods     = new Dictionary<string, MethodInfo>();
            var controllerType    = typeof(TServiceController);
            var clientType        = client.GetType();
            var sbError           = new StringBuilder();

            // Ensure that the service controller and client route templates match.

            var generatedClientAttribute = clientType.GetCustomAttribute<GeneratedClientAttribute>();
            var controllerRouteAttribute = controllerType.GetCustomAttribute<Microsoft.AspNetCore.Mvc.RouteAttribute>();

            if (generatedClientAttribute == null)
            {
                sbError.AppendLine($"ERROR: [{clientType.Name}] must be tagged with a [GeneratedClient] attribute.");
            }

            if (controllerRouteAttribute == null)
            {
                sbError.AppendLine($"ERROR: [{controllerType.Name}] must be tagged with a [Route] attribute.");
            }

            if (generatedClientAttribute != null && controllerRouteAttribute != null)
            {
                if (generatedClientAttribute.RouteTemplate != controllerRouteAttribute.Template)
                {
                    sbError.AppendLine($"ERROR: [{controllerType.Name}] has [Route(\"{generatedClientAttribute.RouteTemplate}\")] which does not match [{clientType.Name}]'s [Route(\"{generatedClientAttribute.RouteTemplate}\")].");
                }
            }

            // Load the controller methods that call service endpoints.

            foreach (var method in clientType.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            {
                var noValidationAttribute = method.GetCustomAttribute<NoValidationAttribute>();

                if (noValidationAttribute != null)
                {
                    continue;
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

                var httpMethod      = "GET";
                var methodAttribute = method.GetCustomAttribute<HttpAttribute>();
                var methodName      = method.Name.ToUpperInvariant();

                if (methodAttribute != null)
                {
                    httpMethod = methodAttribute.HttpMethod;
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

                if (!controllerMethods.TryGetValue(signature, out var existing))
                {
                    sbError.AppendLine($"ERROR: [{controllerType.Name}] has multiple methods including [{method.Name}(...)] and [{existing.Name}(...)] with the same parameters at endpoint [{httpMethod}:{routeTemplate}].");
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

                if (!clientMethods.TryGetValue(signature, out var existing))
                {
                    sbError.AppendLine($"ERROR: [{clientType.Name}] has multiple methods including [{method.Name}(...)] and [{existing.Name}(...)] with the same parameters at endpoint [{generatedMethodAttribute.HttpMethod}:{generatedMethodAttribute.RouteTemplate}].");
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
                    sbError.AppendLine($"ERROR: Service controller [{controllerType.Name}] lacks the method that matches [{clientType.Name}.{clientMethod.Value}] with signature [{clientMethod.Key}].");
                }
            }

            // Ensure that all of the controller methods are also present in the client.

            foreach (var controllerMethod in controllerMethods)
            {
                if (!clientMethods.ContainsKey(controllerMethod.Key))
                {
                    sbError.AppendLine($"ERROR: Service client [{clientType.Name}] lacks the method that matches [{controllerType.Name}.{controllerMethod.Value}] with signature [{controllerMethod.Key}].");
                }
            }

            // Do a detailed comparision of the method return types and parameters.

            foreach (var clientMethod in clientMethods)
            {
                if (controllerMethods.TryGetValue(clientMethod.Key, out var controllerMethod))
                {
                    if (clientMethod.Value.ReturnType.FullName != controllerMethod.ReturnType.FullName)
                    {
                        sbError.AppendLine($"ERROR: Service client [{clientType.Name}.{clientMethod}(...)] returns a different type than [{controllerType.Name}.{controllerMethod}].");
                    }

                    var clientParams     = clientMethod.Value.GetParameters();
                    var controllerParams = controllerMethod.GetParameters();

                    for (int i = 0; i < clientParams.Length; i++)
                    {
                        var clientParam     = clientParams[i];
                        var controllerParam = controllerParams[i];

                        var generatedClientParam = clientParam.GetCustomAttribute<GeneratedParamAttribute>();

                        if (generatedClientParam == null)
                        {
                            // Only the leading client method parameters that are actually passed to the
                            // service have this attribute, so we can stop the checks when we see this.

                            break;
                        }

                        // Ensure that parameter optionality is consistent.

                        if (clientParam.IsOptional != controllerParam.IsOptional)
                        {
                            if (controllerParam.IsOptional && !clientParam.IsOptional)
                            {
                                sbError.AppendLine($"ERROR: [{clientType.Name}.{clientMethod}(...)] parameter [{i + 1}] is optional which conflicts with the service definition which is not optional.");
                            }
                            else
                            {
                                sbError.AppendLine($"ERROR: [{clientType.Name}.{clientMethod}(...)] parameter [{i + 1}] is not optional which conflicts with the service definition which is optional.");
                            }
                        }

                        if (clientParam.IsOptional && !clientParam.DefaultValue.Equals(controllerParam.DefaultValue))
                        {
                            sbError.AppendLine($"ERROR: [{clientType.Name}.{clientMethod}(...)] parameter [{i + 1}] default value [{clientParam.DefaultValue}] does not match the controller default value [{controllerParam.DefaultValue}].");
                        }

                        // Ensure that the method for transmitting the parameter is consistent.

                        var fromQueryAttribute = controllerParam.GetCustomAttribute<Microsoft.AspNetCore.Mvc.FromQueryAttribute>();

                        if (fromQueryAttribute != null)
                        {
                            if (generatedClientParam.PassAs != PassAs.Query)
                            {
                                sbError.AppendLine($"ERROR: [{clientType.Name}.{clientMethod}(...)] parameter [{i + 1}] is passed as [From{generatedClientParam.PassAs}] which doesn't match the controller method parameter which uses [FromQuery].");
                            }
                            else if (generatedClientParam.Name != fromQueryAttribute.Name)
                            {
                                sbError.AppendLine($"ERROR: [{clientType.Name}.{clientMethod}(...)] parameter [{i + 1}] is passed using name [{generatedClientParam.Name}] which doesn't match the controller methid parameter which uses [{fromQueryAttribute.Name}].");
                            }
                        }
                        else
                        {
                            var fromRouteAttribute = controllerParam.GetCustomAttribute<Microsoft.AspNetCore.Mvc.FromRouteAttribute>();

                            if (fromRouteAttribute != null)
                            {
                                if (generatedClientParam.PassAs != PassAs.Route)
                                {
                                    sbError.AppendLine($"ERROR: [{clientType.Name}.{clientMethod}(...)] parameter [{i + 1}] is passed as [From{generatedClientParam.PassAs}] which doesn't match the controller method parameter which uses [FromRoute].");
                                }
                                else if (generatedClientParam.Name != fromRouteAttribute.Name)
                                {
                                    sbError.AppendLine($"ERROR: [{clientType.Name}.{clientMethod}(...)] parameter [{i + 1}] is passed using name [{generatedClientParam.Name}] which doesn't match the controller methid parameter which uses [{fromRouteAttribute.Name}].");
                                }
                            }
                            else
                            {
                                var fromHeaderAttribute = controllerParam.GetCustomAttribute<Microsoft.AspNetCore.Mvc.FromHeaderAttribute>();

                                if (fromHeaderAttribute != null)
                                {
                                    if (generatedClientParam.PassAs != PassAs.Header)
                                    {
                                        sbError.AppendLine($"ERROR: [{clientType.Name}.{clientMethod}(...)] parameter [{i + 1}] is passed as [From{generatedClientParam.PassAs}] which doesn't match the controller method parameter which uses [FromHeader].");
                                    }
                                    else if (generatedClientParam.Name != fromHeaderAttribute.Name)
                                    {
                                        sbError.AppendLine($"ERROR: [{clientType.Name}.{clientMethod}(...)] parameter [{i + 1}] is passed using name [{generatedClientParam.Name}] which doesn't match the controller methid parameter which uses [{fromHeaderAttribute.Name}].");
                                    }
                                }
                                else
                                {
                                    var fromBodyAttribute = controllerParam.GetCustomAttribute<Microsoft.AspNetCore.Mvc.FromBodyAttribute>();

                                    if (fromBodyAttribute != null)
                                    {
                                        if (generatedClientParam.PassAs != PassAs.Body)
                                        {
                                            sbError.AppendLine($"ERROR: [{clientType.Name}.{clientMethod}(...)] parameter [{i + 1}] is passed as [From{generatedClientParam.PassAs}] which doesn't match the controller method parameter which uses [FromBody].");
                                        }
                                    }
                                    else
                                    {
                                        // We default to [FromQuery] when nothing else is specified.

                                        if (generatedClientParam.PassAs != PassAs.Query)
                                        {
                                            sbError.AppendLine($"ERROR: [{clientType.Name}.{clientMethod}(...)] parameter [{i + 1}] is passed as [From{generatedClientParam.PassAs}] which doesn't match the controller method parameter which implicitly specifies [FromQuery].");
                                        }
                                        else if (generatedClientParam.Name != controllerParam.Name)
                                        {
                                            sbError.AppendLine($"ERROR: [{clientType.Name}.{clientMethod}(...)] parameter [{i + 1}] is passed using name [{generatedClientParam.Name}] which doesn't match the controller methid parameter which uses [{controllerParam.Name}].");
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
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

            sbSignature.Append($"{httpMethod}:{method.ReturnType.ToString()} {routeTemplate}(");

            var firstParam = true;

            foreach (var parameter in method.GetParameters())
            {
                if (requireGeneratedParamAttribute)
                {
                    var generatedMethodAttribute = parameter.GetCustomAttribute<GeneratedMethodAttribute>();

                    if (generatedMethodAttribute == null)
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

                sbSignature.Append(parameter.ParameterType.ToString());
            }

            sbSignature.Append(")");

            return sbSignature.ToString();
        }
    }
}
