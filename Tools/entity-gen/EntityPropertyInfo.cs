//-----------------------------------------------------------------------------
// FILE:	    EntityPropertyInfo.cs
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
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

using Neon.Common;
using Neon.DynamicData;

namespace EntityGen
{
    /// <summary>
    /// Information about an entity property.
    /// </summary>
    public class EntityPropertyInfo
    {
        private CodeGenerator       task;
        private EntityInfo          parentEntity;
        private bool                ignoreErrors;

        /// <summary>
        /// Private constructor.
        /// </summary>
        private EntityPropertyInfo()
        {
        }

        /// <summary>
        /// Constructs the information from the .NET property information.
        /// </summary>
        /// <param name="parentEntity">The parent entity definition.</param>
        /// <param name="propInfo">The .NET property information.</param>
        /// <param name="task">The build task.</param>
        /// <param name="ignoreErrors">Pass <c>true</c> to ignore any definition errors.</param>
        public EntityPropertyInfo(EntityInfo parentEntity, PropertyInfo propInfo, CodeGenerator task, bool ignoreErrors = false)
        {
            this.parentEntity = parentEntity;
            this.task         = task;
            this.ignoreErrors = ignoreErrors;

            var propertyAttribute = propInfo.GetCustomAttribute<DynamicEntityPropertyAttribute>(inherit: false);

            PropertyName = propInfo.Name;
            JsonName     = PropertyName;
            PropertyType = propInfo.PropertyType;

            if (propertyAttribute != null)
            {
                IsLink = propertyAttribute.IsLink;

                if (!string.IsNullOrWhiteSpace(propertyAttribute.Name))
                {
                    if (JsonName.IndexOfAny(new char[] { '"', '\'', '\r', '\n' }) != -1)
                    {
                        LogError($"[{parentEntity.Interface.FullName}.{propInfo.Name}]: specifies an invalid JSON serialization name.");
                        return;
                    }
                    else
                    {
                        JsonName = propertyAttribute.Name;
                    }
                }
                else if (propertyAttribute.IsTypeProperty)
                {
                    JsonName = DynamicEntity.EntityTypeName;
                }

                if (propertyAttribute.IsLink && propertyAttribute.IsTypeProperty)
                {
                    LogError($"[{parentEntity.Interface.FullName}.{propInfo.Name}]: cannot specify [IsLink=true] and [IsTypeProperty=true] at the same time.");
                }

                if (propertyAttribute.IsTypeProperty)
                {
                    IsTypeProperty = true;

                    if (!string.IsNullOrEmpty(propertyAttribute.Name))
                    {
                        LogError($"[{parentEntity.Interface.FullName}.{propInfo.Name}]: cannot specify [IsTypeProperty=true] and [Name] at the same time.  You probably want to delete the [Name] argument.");
                    }

                    if (parentEntity.IsDerived)
                    {
                        LogError($"[{parentEntity.Interface.FullName}.{propInfo.Name}]: cannot specify [IsTypeProperty=true] because the entity interface inherits from another.");
                    }

                    if (parentEntity.Attribute.Type == null)
                    {
                        LogError($"[{parentEntity.Interface.FullName}.{propInfo.Name}]: cannot be defined with [IsTypeProperty=true] because the entity interface does not set [{nameof(DynamicEntityAttribute)}.{nameof(DynamicEntityAttribute.Type)}].");
                    }
                    else
                    {
                        var propertyTypeString = parentEntity.Attribute.Type as string;

                        if (propertyTypeString != null)
                        {
                            if (propInfo.PropertyType != typeof(string))
                            {
                                LogError($"[{parentEntity.Interface.FullName}.{propInfo.Name}]: is not type compatible with [{nameof(DynamicEntityAttribute)}.{nameof(DynamicEntityAttribute.Type)}] value.");
                            }

                            TypePropertyValue = $"\"{propertyTypeString}\"";
                        }
                        else
                        {
                            // Must be an enum.

                            if (parentEntity.Attribute.Type.GetType() != propInfo.PropertyType)
                            {
                                LogError($"[{parentEntity.Interface.FullName}.{propInfo.Name}]: is not type compatible with [{nameof(DynamicEntityAttribute)}.{nameof(DynamicEntityAttribute.Type)}] value.");
                            }

                            TypePropertyValue = $"{propInfo.PropertyType.FullName}.{parentEntity.Attribute.Type}";
                        }
                    }

                    if (propInfo.GetMethod == null)
                    {
                        LogError($"[{parentEntity.Interface.FullName}.{propInfo.Name}]: must have a getter.");
                    }

                    if (propInfo.SetMethod != null)
                    {
                        LogError($"[{parentEntity.Interface.FullName}.{propInfo.Name}]: was defined with [IsTypeProperty=true] so it cannot have a setter.");
                    }
                }
                else
                {
                    if (propInfo.GetMethod == null || propInfo.SetMethod == null)
                    {
                        LogError($"[{parentEntity.Interface.FullName}.{propInfo.Name}]: must have both a getter and a setter.");
                    }
                }
            }

            var propertyType = propInfo.PropertyType;

            if (propertyType.GetTypeInfo().IsGenericType)
            {
                LogError($"[{parentEntity.Interface.FullName}.{propInfo.Name}]: has an unsupported generic type.");
                return;
            }

            if (propertyType.IsArray)
            {
                IsArray = true;

                if (propertyType.GetArrayRank() > 1)
                {
                    LogError($"[{parentEntity.Interface.FullName}.{propInfo.Name}]: is an unsupported multi-dimensional array.");
                    return;
                }

                var elementType     = propertyType.GetElementType();
                var elementTypeName = GetTypeName(elementType);

                if (elementTypeName == null)
                {
                    LogError($"[{parentEntity.Interface.FullName}.{propInfo.Name}]: has an unsupported array element type.");
                    return;
                }

                bool isEntity;

                if (GetEntityTargetName(elementType, out isEntity) != null)
                {
                    if (isEntity)
                    {
                        // Array element is an entity.

                        if (IsLink)
                        {
                            MapperType = $"LinkListMapper<{elementTypeName}>";
                        }
                        else
                        {
                            MapperType = $"EntityListMapper<{elementTypeName}>";
                        }
                    }
                    else
                    {
                        // Array element must be a document link.  Note that we're going to ignore the [IsLink]
                        // property and always generate a link (bacause nothing else makes sense).

                        IsLink     = true;
                        MapperType = $"DocListMapper<{elementTypeName}>";
                    }
                }
                else
                {
                    // Array element is a built-in type.

                    MapperType = $"ListMapper<{elementTypeName}>";
                }

                MappedItemType     = elementTypeName;
                PublicType         = $"IList<{elementTypeName}>";
                NotEqualExpression = $"!NeonHelper.SequenceEqual(this.{PropertyName}, other.{PropertyName})";
            }
            else
            {
                var typeName = GetTypeName(propertyType);

                if (typeName == null)
                {
                    LogError($"[{parentEntity.Interface.FullName}.{propInfo.Name}]: has an unsupported type.");
                    return;
                }

                bool isEntity;

                if (GetEntityTargetName(propertyType, out isEntity) != null)
                {
                    if (isEntity)
                    {
                        // Property is an entity.

                        if (IsLink)
                        {
                            MapperType = $"LinkMapper<{typeName}>";
                        }
                        else
                        {
                            MapperType = $"EntityMapper<{typeName}>";
                        }
                    }
                    else
                    {
                        // Property must be a document.  Note that we're going to ignore the [IsLink]
                        // property and always generate a link (bacause nothing else makes sense).

                        IsLink     = true;
                        MapperType = $"DocLinkMapper<{typeName}>";
                    }
                }
                else
                {
                    // Property is a built-in type.

                    MapperType = $"SimpleMapper<{typeName}>";
                }

                MappedItemType = typeName;
                PublicType     = typeName;

                if (IsLink)
                {
                    NotEqualExpression = $"this._{PropertyName}.Link != other._{PropertyName}.Link";
                }
                else
                {
                    NotEqualExpression = $"this.{PropertyName} != other.{PropertyName}";
                }
            }

            IsValid = true;
        }

        /// <summary>
        /// Logs an error message.
        /// </summary>
        /// <param name="message">The message.</param>
        private void LogError(string message)
        {
            if (ignoreErrors)
            {
                return;
            }

            task.Log.LogError(message);
        }

        /// <summary>
        /// Returns the C# keyword or type name for a property type.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <returns>The keyword or type name for simple </returns>
        private string GetTypeName(Type type)
        {
            if (type.GetTypeInfo().IsEnum)
            {
                var includedType = task.GetIncludedType(type);

                if (includedType != null)
                {
                    return includedType.FullName;
                }
                else
                {
                    return type.FullName;
                }
            }

            switch (Type.GetTypeCode(type))
            {
                case TypeCode.Boolean:  return "bool";
                case TypeCode.Byte:     return "byte";
                case TypeCode.Char:     return "char";
                case TypeCode.DateTime: return "DateTime";
                case TypeCode.Decimal:  return "decimal";
                case TypeCode.Double:   return "double";
                case TypeCode.Int16:    return "short";
                case TypeCode.Int32:    return "int";
                case TypeCode.Int64:    return "long";
                case TypeCode.SByte:    return "signed byte";
                case TypeCode.Single:   return "float";
                case TypeCode.String:   return "string";
                case TypeCode.UInt16:   return "unsigned short";
                case TypeCode.UInt32:   return "unsigned int";
                case TypeCode.UInt64:   return "unsigned long";

                case TypeCode.Object:

                    if (type == typeof(DateTime))
                    {
                        return "DateTime";
                    }
                    else if (type == typeof(DateTimeOffset))
                    {
                        return "DateTimeOffset";
                    }
                    else if (type == typeof(TimeSpan))
                    {
                        return "TimeSpan";
                    }
                    else if (type == typeof(Guid))
                    {
                        return "Guid";
                    }
                    else
                    {
                        bool isEntity;

                        return GetEntityTargetName(type, out isEntity);
                    }

                default:

                    return null;
            }
        }

        /// <summary>
        /// Returns the target name for an entity type.
        /// </summary>
        /// <param name="type">The entity type.</param>
        /// <param name="isEntity">Returns <c>true</c> for entity types, <c>false</c> for binder types.</param>
        /// <returns>The target name or <c>null</c> if the entity doesn't exist.</returns>
        private string GetEntityTargetName(Type type, out bool isEntity)
        {
            isEntity = false;

            if (type == this.parentEntity.Interface)
            {
                // This is a special case where an entity property references the entity itself.

                isEntity = true;
                return parentEntity.FullName;
            }
            else
            {
                var typeName = task.FindEntity(type.FullName)?.FullName;

                if (typeName != null)
                {
                    isEntity = true;
                    return typeName;
                }

                return task.FindBinder(type.FullName)?.FullName;
            } 
        }

        /// <summary>
        /// Clones the current instance by copying the public properties.
        /// </summary>
        /// <returns>The cloned <see cref="EntityPropertyInfo"/>.</returns>
        public EntityPropertyInfo Clone()
        {
            // WARNING: Be sure to update if you add/remove public properties.

            return new EntityPropertyInfo()
            {
                IsValid            = this.IsValid,
                IsDerived          = this.IsDerived,
                PropertyName       = this.PropertyName,
                JsonName           = this.JsonName,
                PropertyType       = this.PropertyType,
                IsArray            = this.IsArray,
                MapperType         = this.MapperType,
                MappedItemType     = this.MappedItemType,
                PublicType         = this.PublicType,
                NotEqualExpression = this.NotEqualExpression,
                IsTypeProperty     = this.IsTypeProperty,
                TypePropertyValue  = this.TypePropertyValue
            };
        }

        /// <summary>
        /// Returns <c>true</c> if the property is valid.
        /// </summary>
        public bool IsValid { get; private set; }

        /// <summary>
        /// Set to <c>true</c> if the property is derived from an ancestor interface.
        /// </summary>
        public bool IsDerived { get; set; }

        /// <summary>
        /// Returns the entity property name.
        /// </summary>
        public string PropertyName { get; private set; }

        /// <summary>
        /// Returns the JSON property name.
        /// </summary>
        public string JsonName { get; private set; }

        /// <summary>
        /// Returns the property type.
        /// </summary>
        public Type PropertyType { get; private set; }

        /// <summary>
        /// Returns <c>true</c> if the property is a link to another entity.
        /// </summary>
        public bool IsLink { get; private set; }

        /// <summary>
        /// Returns <c>true</c> if the property is an array.
        /// </summary>
        public bool IsArray { get; private set; }

        /// <summary>
        /// The private type to use when generating the property mapper.
        /// </summary>
        public string MapperType { get; private set; }

        /// <summary>
        /// Returns the mapper's item type.  This is the property type for simple properties
        /// or the element type for arrays.
        /// </summary>
        public string MappedItemType { get; private set; }

        /// <summary>
        /// Returns the public type.
        /// </summary>
        public string PublicType { get; private set; }

        /// <summary>
        /// The expression to be used to compare this property with the same
        /// property in another instance.  This assumes that the source generated
        /// names the other instance <b>other</b> and the expression returns 
        /// <c>true</c> if the instances are not equal.
        /// </summary>
        public string NotEqualExpression { get; private set; }

        /// <summary>
        /// Indicates that this is the special read-only entity type property.
        /// </summary>
        public bool IsTypeProperty { get; private set; }

        /// <summary>
        /// Returns the entity type property's value as string.
        /// </summary>
        public string TypePropertyValue { get; set; }
    }
}
