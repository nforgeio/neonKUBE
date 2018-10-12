//-----------------------------------------------------------------------------
// FILE:	    EntityInfo.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using System.Runtime.Serialization;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

using Neon.Common;
using Neon.DynamicData;

namespace EntityGen
{
    /// <summary>
    /// Information about an entity.
    /// </summary>
    public class EntityInfo
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Returns the entity type string for an entity interface type.
        /// </summary>
        /// <param name="interfaceType">The entity interface definition.</param>
        /// <param name="task">The build task.</param>
        /// <returns>The entity type string or <c>null</c>.</returns>
        private static string GetEntityType(Type interfaceType, CodeGenerator task)
        {
            var entityTypeString = (string)null;
            var entityAttribute  = interfaceType.GetTypeInfo().GetCustomAttribute<DynamicEntityAttribute>(inherit: false);

            if (entityAttribute.Type != null)
            {
                var typeString = entityAttribute.Type as string;

                if (typeString != null)
                {
                    if (!string.IsNullOrWhiteSpace(typeString))
                    {
                        entityTypeString = typeString;
                    }
                }
                else
                {
                    var enumType = entityAttribute.Type.GetType();

                    if (!enumType.GetTypeInfo().IsEnum)
                    {
                        task.Log.LogError($"Entity [{interfaceType.FullName}] has a [{nameof(DynamicEntityAttribute)}] with a [{nameof(DynamicEntityAttribute.Type)}] value that is not a string or enum value.");
                    }
                    else
                    {
                        var enumMemberAttribute = (EnumMemberAttribute)enumType
                                                  .GetMember(entityAttribute.Type.ToString()).Single()
                                                  .GetCustomAttribute<EnumMemberAttribute>(inherit: false);

                        if (enumMemberAttribute != null)
                        {
                            entityTypeString = enumMemberAttribute.Value;
                        }
                        else
                        {
                            entityTypeString = entityAttribute.Type.ToString();
                        }
                    }
                }
            }

            return entityTypeString;
        }

        //---------------------------------------------------------------------
        // Instance members

        private Type    interfaceType;

        /// <summary>
        /// Constructs the entity information from the entity interface definition.
        /// </summary>
        /// <param name="interfaceType">The entity interface definition.</param>
        /// <param name="entityAttribute">The definition's <see cref="DynamicEntityAttribute"/>.</param>
        /// <param name="task">The build task.</param>
        public EntityInfo(Type interfaceType, DynamicEntityAttribute entityAttribute, CodeGenerator task)
        {
            this.interfaceType  = interfaceType;
            this.Attribute      = entityAttribute;

            var targetNamespace = interfaceType.Namespace;
            var targetName      = interfaceType.Name;

            if (targetName.StartsWith("I"))
            {
                targetName = targetName.Substring(1);
            }

            if (!string.IsNullOrWhiteSpace(entityAttribute.Namespace))
            {
                targetNamespace = entityAttribute.Namespace.Trim();
            }

            if (!string.IsNullOrWhiteSpace(entityAttribute.Name))
            {
                targetName = entityAttribute.Name.Trim();
            }

            var entityType       = GetEntityType(interfaceType, task);
            var entityTypeString = "null";

            if (entityType != null)
            {
                entityTypeString = $"\"{entityType}\"";
            }

            if (entityTypeString.Contains(":"))
            {
                task.Log.LogError($"Entity [{interfaceType.FullName}] has a [{nameof(DynamicEntityAttribute)}] with a [{nameof(DynamicEntityAttribute.Type)}] value that includes a colon (:).");
            }

            var entityTypePathList = new List<string>();

            if (entityType != null)
            {
                entityTypePathList.Add(entityType);

                for (var parentType = interfaceType.GetParentInterface(); parentType != null; parentType = parentType.GetParentInterface())
                {
                    var parentEntityType = GetEntityType(parentType, task);

                    if (parentEntityType != null)
                    {
                        entityTypePathList.Add(parentEntityType);
                    }
                }

                EntityTypePath = entityTypePathList.ToArray();
            }

            Interface  = interfaceType;
            IsDerived  = interfaceType.GetParentInterface() != null;
            Namespace  = targetNamespace;
            Name       = targetName;
            FullName   = $"{targetNamespace}.{targetName}";
            TypeLiteral = entityTypeString;
            Visibility = entityAttribute.IsInternal ? "internal" : "public";
        }

        /// <summary>
        /// Loads the entity properties.
        /// </summary>
        /// <param name="task">The build task.</param>
        public void LoadProperties(CodeGenerator task)
        {
            foreach (var propInfo in interfaceType.GetProperties())
            {
                var property = new EntityPropertyInfo(this, propInfo, task);

                if (property.IsValid)
                {
                    Properties.Add(property);
                }
            }

            // We need to add ancestor properties too so we can generate static all-inclusive
            // property/name maps.

            for (var parentType = interfaceType.GetParentInterface(); parentType != null; parentType = parentType.GetParentInterface())
            {
                foreach (var propInfo in parentType.GetProperties(BindingFlags.DeclaredOnly))
                {
                    var property = new EntityPropertyInfo(this, propInfo, task, ignoreErrors: true);

                    if (property.IsValid)
                    {
                        property.IsDerived = true;
                        Properties.Add(property);
                    }
                }
            }

            var typePropertyCount = Properties.Count(p => p.IsTypeProperty && !p.IsDerived);
            var parentInterface = interfaceType.GetParentInterface();

            if (parentInterface != null && typePropertyCount > 0)
            {
                task.Log.LogError($"Entity [{interfaceType.FullName}] is not a base interface so it cannot have a property tagged with [IsTypeProperty=true].");
            }

            if (parentInterface == null && typePropertyCount > 1)
            {
                task.Log.LogError($"Entity [{interfaceType.FullName}] has more than one property tagged with [IsTypeProperty=true].");
            }

            // Sort the properties by entity name to make the generated code
            // more consistent and easier to read.

            Properties = Properties.OrderBy(p => p.PropertyName, StringComparer.OrdinalIgnoreCase).ToList();
        }

        /// <summary>
        /// Performs any necessary processing to resolve references between entity definitions.
        /// This should be called after the first pass at processing all definitions.
        /// </summary>
        /// <param name="definitions">The entity definitions keyed by source full type name.</param>
        /// <param name="includedTypes">The included type definitions keyed by source full type name.</param>
        public void ResolveParents(Dictionary<string, EntityInfo> definitions, Dictionary<Type, IncludedType> includedTypes)
        {
            // For root entity interfaces, we need to set [EntityTypeProperty] to the tagged
            // property, if it has one.

            foreach (var rootDefinition in definitions.Values.Where(d => !d.IsDerived))
            {
                rootDefinition.EntityTypeProperty = rootDefinition.Properties.SingleOrDefault(p => p.IsTypeProperty);

                if (rootDefinition.EntityTypeProperty == null)
                {
                    continue;
                }

                var propertyTypeString = rootDefinition.Attribute.Type as string;

                if (propertyTypeString != null)
                {
                    rootDefinition.EntityTypeProperty.TypePropertyValue = $"\"{propertyTypeString}\"";
                }
                else
                {
                    // Must be an enum.

                    // $hack(jeff.lill)
                    //
                    // I didn't have the mapped entity type earlier when I parsed the entity properties.

                    var includedType = includedTypes[rootDefinition.EntityTypeProperty.PropertyType];

                    rootDefinition.EntityTypeProperty.TypePropertyValue = $"{includedType.FullName}.{rootDefinition.Attribute.Type}";
                }
            }

            // For derived entity interfaces, we need to set [EntityTypeProperty] to a clone
            // of the tagged property from the root interface, if it has one with its
            // [TypePropertyValue] set to the value from this definition.

            foreach (var derivedDefinition in definitions.Values.Where(d => d.IsDerived))
            {
                var rootInterface  = derivedDefinition.Interface.GetRootInterface();
                var rootDefinition = definitions[rootInterface.FullName];

                if (rootDefinition.EntityTypeProperty == null)
                {
                    continue;
                }

                // $hack(jeff.lill):
                //
                // This is a bit of a hack that should be encapsulated into [EntityPropertyInfo]
                // better, but I need to finish this and move on.

                var entityTypeProperty = rootDefinition.EntityTypeProperty.Clone();
                var propertyTypeString = rootDefinition.Attribute.Type as string;

                if (propertyTypeString != null)
                {
                    entityTypeProperty.TypePropertyValue = $"\"{propertyTypeString}\"";
                }
                else
                {
                    // Must be an enum.

                    entityTypeProperty.TypePropertyValue = $"{entityTypeProperty.MappedItemType}.{derivedDefinition.Attribute.Type}";
                }

                derivedDefinition.EntityTypeProperty = entityTypeProperty;
            }
        }

        /// <summary>
        /// The defining <c>interface</c>.
        /// </summary>
        public Type Interface { get; private set; }

        /// <summary>
        /// Returns the <see cref="DynamicEntityAttribute"/> used to tag the entity interface.
        /// </summary>
        public DynamicEntityAttribute Attribute { get; private set; }

        /// <summary>
        /// Returns the target namespace.
        /// </summary>
        public string Namespace { get; private set; }

        /// <summary>
        /// Returns the unqualified target entity type.
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// Returns the fully qualified target entity type.
        /// </summary>
        public string FullName { get; private set; }

        /// <summary>
        /// Returns the entity type as string C# string literal or <b>"null"</b>.
        /// </summary>
        public string TypeLiteral { get; private set; }

        /// <summary>
        /// Returns <c>true</c> if the entity derives from another.
        /// </summary>
        public bool IsDerived { get; private set; }

        /// <summary>
        /// Returns the entity visibility: <b>public</b> or <b>internal</b>.
        /// </summary>
        public string Visibility { get; private set; }

        /// <summary>
        /// Returns the property information.
        /// </summary>
        public List<EntityPropertyInfo> Properties { get; private set; } = new List<EntityPropertyInfo>();

        /// <summary>
        /// Returns the information for the property acting as the special entity
        /// type property (ie. it was tagged with [IsTypeProperty=true] or NULL if
        /// there is no such property.  Note that this property will be from the
        /// root entity definition for derived entities.
        /// </summary>
        public EntityPropertyInfo EntityTypeProperty { get; private set; }

        /// <summary>
        /// Returns the entity's type path as a string array creator or <c>null</c>.
        /// </summary>
        public string[] EntityTypePath { get; private set; }

        /// <summary>
        /// Returns the <see cref="EntityTypePath"/> as a C# string literal with the array
        /// elements separated by colons (<b>:</b>) or "null".
        /// </summary>
        public string EntityTypePathLiteral
        {
            get
            {
                if (EntityTypePath == null || EntityTypePath.Length == 0)
                {
                    return "null";
                }

                var sb = new StringBuilder();

                foreach (var item in EntityTypePath)
                {
                    sb.AppendWithSeparator(item, ":");
                }

                return $"\"{sb.ToString()}\"";
            }
        }
    }
}
