//-----------------------------------------------------------------------------
// FILE:	    BinderInfo.cs
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
    /// Information about a binder document.
    /// </summary>
    public class BinderInfo
    {
        /// <summary>
        /// Constructs the binder information from the binder document interface definition.
        /// </summary>
        /// <param name="interfaceType">The binder document interface definition.</param>
        /// <param name="binderAttribute">The definition's <see cref="BinderDocumentAttribute"/>.</param>
        /// <param name="entityDefinitions">The entity definitions keyed by fully qualified defining interface name.</param>
        /// <param name="task">The build task.</param>
        public BinderInfo(Type interfaceType, BinderDocumentAttribute binderAttribute, Dictionary<string, EntityInfo> entityDefinitions, CodeGenerator task)
        {
            var targetNamespace = interfaceType.Namespace;
            var targetName      = interfaceType.Name;

            if (targetName.StartsWith("I"))
            {
                targetName = targetName.Substring(1);
            }

            if (!string.IsNullOrWhiteSpace(binderAttribute.Namespace))
            {
                targetNamespace = binderAttribute.Namespace.Trim();
            }

            if (!string.IsNullOrWhiteSpace(binderAttribute.Name))
            {
                targetName = binderAttribute.Name.Trim();
            }

            Interface  = interfaceType;
            Namespace  = targetNamespace;
            Name       = targetName;
            FullName   = $"{targetNamespace}.{targetName}";
            Visibility = binderAttribute.IsInternal ? "internal" : "public";

            // Perform some binder document checks.

            if (interfaceType.GetInterfaces().Length > 0)
            {
                task.Log.LogError($"Binder document interface [{interfaceType.FullName}] implements another interface.  This is not allowed.");
            }

            EntityInfo targetEntityDefinition;

            if (entityDefinitions.TryGetValue(binderAttribute.EntityType.FullName, out targetEntityDefinition))
            {
                TargetEntityType = targetEntityDefinition.FullName;
            }
            else
            {
                task.Log.LogError($"Binder document interface [{interfaceType.FullName}] references the entity type [{binderAttribute.EntityType.FullName}] which is not tagged with [{nameof(DynamicEntityAttribute)}].");
            }

            // Load the binder's attachment information.

            foreach (var propertyInfo in interfaceType.GetProperties())
            {
                var attachmentAttribute = propertyInfo.GetCustomAttribute<BinderAttachmentAttribute>();

                if (attachmentAttribute == null)
                {
                    task.Log.LogWarning($"Binder document interface [{interfaceType.FullName}] defines a [{propertyInfo.Name}] property without a [{nameof(BinderAttachmentAttribute)}].  This property will be ignored.");
                }

                if (propertyInfo.PropertyType != typeof(string))
                {
                    task.Log.LogError($"Binder document interface [{interfaceType.FullName}] defines a [{propertyInfo.Name}] property that is not defined as a [string].");
                }

                if (propertyInfo.SetMethod != null)
                {
                    task.Log.LogError($"Binder document interface [{interfaceType.FullName}] defines a [{propertyInfo.Name}] property with a setter.");
                }

                if (propertyInfo.GetMethod == null)
                {
                    task.Log.LogError($"Binder document interface [{interfaceType.FullName}] defines a [{propertyInfo.Name}] property without a getter");
                }

                if (string.IsNullOrEmpty(attachmentAttribute.AttachmentName))
                {
                    attachmentAttribute.AttachmentName = propertyInfo.Name;
                }

                Attachments.Add(
                    new AttachmentInfo()
                    {
                        PropertyName   = propertyInfo.Name,
                        AttachmentName = attachmentAttribute.AttachmentName ?? propertyInfo.Name
                    });
            }
        }

        /// <summary>
        /// The defining <c>interface</c>.
        /// </summary>
        public Type Interface { get; private set; }

        /// <summary>
        /// Returns the target namespace.
        /// </summary>
        public string Namespace { get; private set; }

        /// <summary>
        /// Returns the unqualified target binder document type.
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// Returns the fully qualified target binder document type.
        /// </summary>
        public string FullName { get; private set; }

        /// <summary>
        /// Returns the binder document visibility: <b>public</b> or <b>internal</b>.
        /// </summary>
        public string Visibility { get; private set; }

        /// <summary>
        /// Returns the fully qualified document target entity type name.
        /// </summary>
        public string TargetEntityType { get; private set; }

        /// <summary>
        /// Returns the binder attachment information.
        /// </summary>
        public List<AttachmentInfo> Attachments { get; private set; } = new List<AttachmentInfo>();
    }
}
