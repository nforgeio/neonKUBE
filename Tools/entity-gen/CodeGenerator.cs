//-----------------------------------------------------------------------------
// FILE:	    CodeGenerator.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

using Neon.Common;
using Neon.DynamicData;

// $todo(jeff.lill): Complete integration and testing of the build task into VS.

namespace EntityGen
{
    /// <summary>
    /// Visual Studio build task that generates <see cref="DynamicEntity"/> based implementations
    /// of data models described as .NET interfaces providing an easy way to map .NET objects into
    /// platforms such as Couchbase or Couchbase Lite.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The <see cref="Sources"/> property of the build task must be set to the file system 
    /// path to the compiled assembly containing one or more interfaces tagged with 
    /// <see cref="DynamicEntityAttribute"/>.  The task will generate a C# source file 
    /// for each tagged interface and add these files to the current build.
    /// </para>
    /// <note>
    /// Entity interface definitions must not derive from another interface and may
    /// not define any methods.
    /// </note>
    /// <para>
    /// Interfaces may define one or more properties, each with a getter and setter.  These properties
    /// may have the following types:
    /// </para>
    /// <list type="table">
    /// <item>
    ///     <term><b>Build-in Types</b></term>
    ///     <description>
    ///     You may define properties with any built-in .NET such as <c>bool</c>, <c>string</c>, <c>int</c>,
    ///     <c>byte</c>, <c>enum</c>, etc.  The only exception to this is <c>object</c>.  Arbitrary <c>object</c>
    ///     properties <b>are not supported</b>.
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><see cref="DateTime"/></term>
    ///     <description>
    ///     Types supported by Newtonsoft.
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><see cref="DateTimeOffset"/></term>
    ///     <description>
    ///     ...
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><see cref="TimeSpan"/></term>
    ///     <description>
    ///     ...
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><see cref="Guid"/></term>
    ///     <description>
    ///     ...
    ///     </description>
    /// </item>
    /// <item>
    ///     <term>interfaces</term>
    ///     <description>
    ///     You may define properties as any interface tagged with the <see cref="DynamicEntityAttribute"/>.
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><c>array</c></term>
    ///     <description>
    ///     You may define array properties form any of the types above.  These will be generated
    ///     as <see cref="IList{T}"/> properties, except for <c>byte[]</c> arrays, which be left
    ///     as is, so JSON.NET will Base64 encode them.
    ///     </description>
    /// </item>
    /// </list>
    /// <para>
    /// Interface properties may be decorated with a <see cref="DynamicEntityPropertyAttribute"/> to
    /// customize the name used to serialize the property value.  This defaults to the property
    /// name as defined in the interface.
    /// </para>
    /// </remarks>
    public class CodeGenerator : Microsoft.Build.Utilities.Task
    {
        /// <summary>
        /// Entity information keyed by fully qualifed defining interface name.
        /// </summary>
        private Dictionary<string, EntityInfo> entityDefinitions = new Dictionary<string, EntityInfo>();

        /// <summary>
        /// The <c>enum</c> and <c>class</c> model types that we're tagged by <see cref="DynamicIncludeAttribute"/>.
        /// The table maps the original type to the some type information.
        /// </summary>
        private Dictionary<Type, IncludedType> includedTypes = new Dictionary<Type, IncludedType>();

        /// <summary>
        /// The binder document definitions keyed by fully qualifed defining interface name.
        /// </summary>
        private Dictionary<string, BinderInfo> binderDefinitions = new Dictionary<string, BinderInfo>();

        /// <summary>
        /// Default constructor.
        /// </summary>
        public CodeGenerator()
        {
        }

        /// <summary>
        /// <b>Required:</b> Paths to the source assemblies that include the entity interface definitions.
        /// More than one assembly can be specified by separating them with semi-colons.
        /// </summary>
        [Required]
        public string[] Sources { get; set; }

        /// <summary>
        /// <b>Required:</b> Path to the generated source code output.
        /// </summary>
        [Required]
        public string Output { get; set; }

        /// <summary>
        /// <para>
        /// <b>Optional:</b> The fully qualified names of the interfaces to be processed
        /// (separated by semi-colons).  You can also specify all interfaces found
        /// within a namespace by appending ".*".  For example: <b>Foo.Bar.*</b> specifies
        /// that interfaces named <b>Foo.Bar.MyModel</b> will be processed.
        /// </para>
        /// <note>
        /// Entities for all interfaces tagged by <see cref="DynamicEntityAttribute"/>
        /// will be generated if this is not specified.
        /// </note>
        /// </summary>
        public string[] Include { get; set; }

        /// <summary>
        /// <b>Optional:</b> The fully qualified name for the <c>static</c> class to be
        /// generated that registers the generated entity classes, preparing them for use.
        /// This defaults to <b>App.Entities</b>.
        /// </summary>
        public string Register { get; set; } = "App.Entities";

        /// <summary>
        /// Executes the build task.
        /// </summary>
        /// <returns><c>true</c> if the operation was successful.</returns>
        public override bool Execute()
        {
            if (Sources.Length == 0 || string.IsNullOrWhiteSpace(Sources[0]))
            {
                Log.LogError("[Sources] property must not be empty.");
                return false;
            }

            // Validate the initialization class name.

            var initClassSegments = Register.Split('.');

            if (initClassSegments.Length == 0)
            {
                Log.LogError($"[{Register}] is not a valid initialization class name: it doesn't specify a namespace.");
            }

            foreach (var segment in initClassSegments)
            {
                // Verify that segments are valid identifiers.

                if (segment.Length == 0 || 
                    !char.IsLetterOrDigit(segment[0]) && segment[0] != '_' || 
                    segment.Count(ch => !char.IsLetterOrDigit(ch) && ch != '_') > 0)
                {
                    Log.LogError($"[{Register}] is not a valid initialization class name.");
                }
            }

            var lastDotPos        = Register.LastIndexOf('.');
            var registerNamespace = Register.Substring(0, lastDotPos);
            var registerClassName = Register.Substring(lastDotPos + 1);

            // Load the included interfaces argument.

            var includedInterfaces = new HashSet<string>();

            if (Include != null && Include.Length > 0)
            {
                foreach (var name in Include)
                {
                    includedInterfaces.Add(name);
                }
            }

            try
            {
                // Load the included types and entity definitions.

                foreach (var assemblyPath in Sources)
                {
                    if (!File.Exists(assemblyPath))
                    {
                        throw new FileNotFoundException($"File [{Path.GetFullPath(assemblyPath)}] was not found.");
                    }

                    var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(assemblyPath);

                    // Load the included types.

                    foreach (var type in assembly.GetExportedTypes().Where(t => t.GetTypeInfo().IsClass || t.GetTypeInfo().IsEnum))
                    {
                        var includeAttribute = type.GetTypeInfo().GetCustomAttribute<DynamicIncludeAttribute>(inherit: false);

                        if (includeAttribute == null)
                        {
                            continue;
                        }

                        includedTypes.Add(type, new IncludedType(type, includeAttribute));
                    }

                    // Load the entity interface definitions.

                    foreach (var type in assembly.GetExportedTypes().Where(t => t.GetTypeInfo().IsInterface))
                    {
                        var entityAttribute = type.GetTypeInfo().GetCustomAttribute<DynamicEntityAttribute>(inherit: false);

                        if (entityAttribute == null)
                        {
                            continue;
                        }

                        if (includedInterfaces.Count == 0 || includedInterfaces.Contains(type.FullName) || includedInterfaces.Contains(type.Namespace + ".*"))
                        {
                            entityDefinitions.Add(type.FullName, new EntityInfo(type, entityAttribute, this));
                        }
                    }

                    // Load the binder document interface definitions.

                    foreach (var type in assembly.GetExportedTypes().Where(t => t.GetTypeInfo().IsInterface))
                    {
                        var binderAttribute = type.GetTypeInfo().GetCustomAttribute<BinderDocumentAttribute>(inherit: false);

                        if (binderAttribute == null)
                        {
                            continue;
                        }

                        binderDefinitions.Add(type.FullName, new BinderInfo(type, binderAttribute, entityDefinitions, this));
                    }

                    // Go back and have each definition load its properties.  We're going this
                    // as a second pass because properties may reference other entities.

                    foreach (var entityDefinition in entityDefinitions.Values)
                    {
                        entityDefinition.LoadProperties(this);
                    }
                }

                // Verify that the specifically requested interfaces actually exist.

                if (includedInterfaces.Count > 0)
                {
                    foreach (var name in includedInterfaces.Where(n => !n.EndsWith(".*")))
                    {
                        if (!entityDefinitions.ContainsKey(name))
                        {
                            Log.LogWarning($"The requested [{name}] entity interface definition does not exist.");
                        }
                    }
                }

                // Verify that any parent interfaces also exist.

                foreach (var entityDefinition in entityDefinitions.Values)
                {
                    var parentInterface = entityDefinition.Interface.GetParentInterface();

                    if (parentInterface != null && !entityDefinitions.ContainsKey(parentInterface.FullName))
                    {
                        Log.LogError($"The [{entityDefinition.Interface.FullName}] interface derives from [{parentInterface.FullName}] which is not selected to be generated.");
                    }
                }

                // Verify that there are no binder document conflicts.

                var binderDictionary = new Dictionary<string, BinderInfo>();

                foreach (var binderDefinition in binderDefinitions.Values)
                {
                    if (binderDictionary.ContainsKey(binderDefinition.FullName))
                    {
                        Log.LogError($"Binder document conflict: [{binderDictionary[binderDefinition.Interface.FullName].FullName}] and [{binderDefinition.Interface.FullName}] both attempt to generate [{binderDefinition.FullName}].");
                    }
                    else
                    {
                        binderDictionary.Add(binderDefinition.FullName, binderDefinition);
                    }
                }

                // Bail out here if there were errors.

                if (Log.HasLoggedErrors)
                {
                    return false;
                }

                // Go through a second pass of processing definitions to resolve references.

                foreach (var entityDefinition in entityDefinitions.Values)
                {
                    entityDefinition.ResolveParents(entityDefinitions, includedTypes);
                }

                // Compute some formatting related values.

                var maxSourceTypeLength = 0;

                foreach (var entityDefinition in entityDefinitions.Values)
                {
                    maxSourceTypeLength = Math.Max(maxSourceTypeLength, entityDefinition.Interface.FullName.Length);
                }

                foreach (var includedType in includedTypes.Values)
                {
                    maxSourceTypeLength = Math.Max(maxSourceTypeLength, includedType.Type.FullName.Length);
                }

                // Verify that the types for entities being generated are unique.

                var typeToEntities = new Dictionary<string, List<string>>();

                foreach (var entityDefinition in entityDefinitions.Values.Where(d => d.TypeLiteral != "null"))
                {
                    List<string> list;

                    if (!typeToEntities.TryGetValue(entityDefinition.TypeLiteral, out list))
                    {
                        typeToEntities.Add(entityDefinition.TypeLiteral, list = new List<string>());
                    }

                    list.Add(entityDefinition.Interface.FullName);
                }

                var reportedFirst = false;

                foreach (var conflict in typeToEntities.Where(item => item.Value.Count > 1))
                {
                    if (!reportedFirst)
                    {
                        Log.LogError($"One or more entity interfaces were defined with conflicting entity types.  These must be unique across the application domain.");
                        reportedFirst = true;
                    }

                    var sbInterfaces = new StringBuilder();

                    foreach (var interfaceName in conflict.Value)
                    {
                        sbInterfaces.AppendWithSeparator($"[{interfaceName}]");
                    }

                    Log.LogError($"Type [{conflict.Key}] is assigned to multiple interfaces: {sbInterfaces}");
                }

                if (Log.HasLoggedErrors)
                {
                    return false;
                }

                // We're generating source code for all of the entities, binder documents, and included types
                // in a single source file.

                // Write the file header and using statements.

                var writer = new StringWriter();

                writer.WriteLine($"//-----------------------------------------------------------------------------");
                writer.WriteLine($"// FILE: {Path.GetFileName(Output)}");
                writer.WriteLine($"//");
                writer.WriteLine($"// This file was generated by the Neon [entity-gen] build tool.");
                writer.WriteLine($"// Any manual edits will be lost when the file is regenerated.");
                writer.WriteLine();
                writer.WriteLine($"#pragma warning disable 1591");
                writer.WriteLine();
                writer.WriteLine($"using System;");
                writer.WriteLine($"using System.Collections.Generic;");
                writer.WriteLine($"using System.Dynamic;");
                writer.WriteLine($"using System.IO;");
                writer.WriteLine();
                writer.WriteLine($"using Newtonsoft.Json.Linq;");
                writer.WriteLine();
                writer.WriteLine($"using Couchbase.Lite;");
                writer.WriteLine();
                writer.WriteLine($"using Neon.Common;");
                writer.WriteLine($"using Neon.DynamicData;");
                writer.WriteLine($"using Neon.DynamicData.Internal;");

                // Append the source code for each entity.

                if (entityDefinitions.Count > 0)
                {
                    writer.WriteLine();
                    writer.WriteLine($"//-----------------------------------------------------------------------------");
                    writer.WriteLine();
                    writer.WriteLine($"#region Entities");
                }

                foreach (var entityDefinition in entityDefinitions.Values.OrderBy(d => d.FullName, StringComparer.OrdinalIgnoreCase))
                {
                    Log.LogMessage($"Generating:   {GetTypeMapVisual(entityDefinition.FullName, entityDefinition.FullName, maxSourceTypeLength)}");

                    if (entityDefinition.Interface.GetTypeInfo().BaseType != null && !entityDefinitions.ContainsKey(entityDefinition.Interface.GetTypeInfo().BaseType.FullName))
                    {
                        Log.LogError($"Interface [{entityDefinition.Interface.FullName}] cannot derive from an interface that not being generated or is not also tagged with [{nameof(DynamicEntityAttribute)}].");
                        continue;
                    }

                    // Determine which class we need to inherit.

                    var parentClass = nameof(DynamicEntity);

                    if (entityDefinition.Interface.GetParentInterface() != null)
                    {
                        var parentDefinition = entityDefinitions[entityDefinition.Interface.GetParentInterface().FullName];

                        parentClass = parentDefinition.FullName;
                    }

                    // Compute some values to make the output format a bit more readable.

                    var mapperTypeWidth   = 0;
                    var mapperMemberWidth = 0;

                    foreach (var property in entityDefinition.Properties.Where(p => !p.IsTypeProperty && !p.IsDerived))
                    {
                        mapperTypeWidth   = Math.Max(mapperTypeWidth, property.MapperType.Length);
                        mapperMemberWidth = Math.Max(mapperMemberWidth, property.PropertyName.Length);
                    }

                    // Generate the source code.

                    writer.WriteLine();
                    writer.WriteLine($"namespace {entityDefinition.Namespace}");
                    writer.WriteLine("{");
                    writer.WriteLine($"    {entityDefinition.Visibility} partial class {entityDefinition.Name} : {parentClass}");
                    writer.WriteLine($"    {{");

                    var entityTypePath = entityDefinition.EntityTypePathLiteral;

                    if (entityTypePath == entityDefinition.TypeLiteral)
                    {
                        entityTypePath = "typeString";
                    }

                    writer.WriteLine($"        //-----------------------------------------------------------------");
                    writer.WriteLine($"        // Static members");
                    writer.WriteLine();
                    writer.WriteLine($"        private const string typeString     = {entityDefinition.TypeLiteral};");
                    writer.WriteLine($"        private const string typePathString = {entityTypePath};");
                    writer.WriteLine();
                    writer.WriteLine($"        private static Dictionary<string, string> propertyNameMap =");
                    writer.WriteLine($"            new Dictionary<string, string>()");
                    writer.WriteLine($"            {{");

                    foreach (var property in entityDefinition.Properties.Where(p => !p.IsTypeProperty))
                    {
                        writer.WriteLine($"                {{ \"{property.JsonName}\", \"{property.PropertyName}\" }},");
                    }
                    writer.WriteLine($"            }};");
                    writer.WriteLine();

                    if (entityDefinition.IsDerived)
                    {
                        writer.WriteLine($"        public static new EntityRegistration _GetRegistration()");
                    }
                    else
                    {
                        writer.WriteLine($"        public static EntityRegistration _GetRegistration()");
                    }

                    writer.WriteLine($"        {{");
                    writer.WriteLine($"            return new EntityRegistration(typeof({entityDefinition.Name}), {entityDefinition.TypeLiteral},");
                    writer.WriteLine($"                (jObject, context) =>");
                    writer.WriteLine($"                {{");
                    writer.WriteLine($"                    return new {entityDefinition.Name}(jObject, context);");
                    writer.WriteLine($"                }});");
                    writer.WriteLine($"        }}");
                    writer.WriteLine();
                    writer.WriteLine($"        public static bool operator ==({entityDefinition.Name} entity1, {entityDefinition.Name} entity2)");
                    writer.WriteLine($"        {{");
                    writer.WriteLine($"            return object.Equals(entity1, entity2);");
                    writer.WriteLine($"        }}");
                    writer.WriteLine();
                    writer.WriteLine($"        public static bool operator !=({entityDefinition.Name} entity1, {entityDefinition.Name} entity2)");
                    writer.WriteLine($"        {{");
                    writer.WriteLine($"            return !object.Equals(entity1, entity2);");
                    writer.WriteLine($"        }}");
                    writer.WriteLine();
                    writer.WriteLine($"        //-----------------------------------------------------------------");
                    writer.WriteLine($"        // Instance members");
                    writer.WriteLine();

                    var privatePropertyCount = 0;

                    foreach (var property in entityDefinition.Properties.Where(p =>!p.IsDerived))
                    {
                        if (property.IsTypeProperty)
                        {
                            continue;
                        }

                        writer.WriteLine($"        private {PadRight(property.MapperType, mapperTypeWidth)} _{property.PropertyName};");
                        privatePropertyCount++;
                    }

                    if (privatePropertyCount > 0)
                    {
                        writer.WriteLine();
                    }

                    writer.WriteLine($"        public {entityDefinition.Name}()");
                    writer.WriteLine($"            : this(new JObject())");
                    writer.WriteLine($"        {{");
                    writer.WriteLine($"        }}");
                    writer.WriteLine();
                    writer.WriteLine($"        public {entityDefinition.Name}(JObject jObject, IEntityContext context = null, bool load = true, Dictionary<string, string> derivedPropertyNameMap = null)");

                    if (entityDefinition.IsDerived)
                    {
                        writer.WriteLine($"            : base(jObject, context: context, load: false, derivedPropertyNameMap: derivedPropertyNameMap)");
                    }
                    else
                    {
                        writer.WriteLine($"            : base(derivedPropertyNameMap ?? propertyNameMap, context)");
                    }

                    writer.WriteLine($"        {{");

                    foreach (var property in entityDefinition.Properties.Where(p => !p.IsDerived))
                    {
                        if (property.IsTypeProperty)
                        {
                            continue;
                        }

                        writer.WriteLine($"            _{PadRight(property.PropertyName, mapperMemberWidth)} = new {property.MapperType}(this, \"{property.JsonName}\", \"{property.PropertyName}\", context);");
                    }

                    writer.WriteLine();
                    writer.WriteLine($"            if (load)");
                    writer.WriteLine($"            {{");
                    writer.WriteLine($"                _Load(jObject, reload: false);");
                    writer.WriteLine($"            }}");
                    writer.WriteLine($"        }}");
                    writer.WriteLine();
                    writer.WriteLine($"        public override string _GetEntityType()");
                    writer.WriteLine($"        {{");
                    writer.WriteLine($"            return typeString;");
                    writer.WriteLine($"        }}");

                    var entityTypeProperty = entityDefinition.EntityTypeProperty;

                    if (entityTypeProperty != null)
                    {
                        var virtualOrOverride = entityDefinition.IsDerived ? "override" : "virtual";

                        writer.WriteLine();
                        writer.WriteLine($"        public {virtualOrOverride} {entityTypeProperty.PublicType} {entityTypeProperty.PropertyName}");
                        writer.WriteLine($"        {{");
                        writer.WriteLine($"            get {{ return {entityTypeProperty.TypePropertyValue}; }}");
                        writer.WriteLine($"        }}");
                    }

                    writer.WriteLine();
                    writer.WriteLine($"        public override bool _Load(JObject jObject, bool reload = false, bool setType = true)");
                    writer.WriteLine($"        {{");
                    writer.WriteLine($"            var changed = false;");
                    writer.WriteLine();

                    if (entityDefinition.IsDerived)
                    {
                        writer.WriteLine($"            changed = base._Load(jObject, reload, setType: false);");
                    }
                    else
                    {
                        writer.WriteLine($"            base._Load(jObject);");
                        writer.WriteLine();
                    }

                    foreach (var property in entityDefinition.Properties.Where(p => !p.IsDerived))
                    {
                        if (property.IsTypeProperty)
                        {
                            continue; // We don't map this one.
                        }

                        writer.WriteLine($"            changed = base.MapProperty<{property.MappedItemType}>(ref _{property.PropertyName}, reload) || changed;");
                    }

                    var setEntityTypePath = entityDefinition.EntityTypePath != null && entityDefinition.EntityTypePath.Length > 0;
                    var setEntityType     = entityTypeProperty != null;

                    if (setEntityTypePath || setEntityType)
                    {
                        writer.WriteLine();
                        writer.WriteLine($"            if (setType)");
                        writer.WriteLine($"            {{");
                    }

                    if (setEntityTypePath)
                    {
                        writer.WriteLine($"                jObject[\"{DynamicEntity.EntityTypePathName}\"] = typePathString;");
                    }

                    if (setEntityType)
                    {
                        writer.WriteLine($"                jObject[\"{entityTypeProperty.JsonName}\"] = typeString;");
                    }

                    if (setEntityTypePath || setEntityType)
                    {
                        writer.WriteLine($"            }}");
                    }

                    writer.WriteLine();
                    writer.WriteLine($"            return changed;");
                    writer.WriteLine($"        }}");
                    writer.WriteLine();
                    writer.WriteLine($"        public override int GetHashCode()");
                    writer.WriteLine($"        {{");
                    writer.WriteLine($"            return base.GetHashCode();");
                    writer.WriteLine($"        }}");
                    writer.WriteLine();
                    writer.WriteLine($"        public override bool Equals(object obj)");
                    writer.WriteLine($"        {{");
                    writer.WriteLine($"            if (object.ReferenceEquals(this, obj))");
                    writer.WriteLine($"            {{");
                    writer.WriteLine($"                return true;");
                    writer.WriteLine($"            }}");

                    if (entityDefinition.IsDerived)
                    {
                        writer.WriteLine();
                        writer.WriteLine($"            if (!base.Equals(obj))");
                        writer.WriteLine($"            {{");
                        writer.WriteLine($"                return false;");
                        writer.WriteLine($"            }}");
                    }

                    writer.WriteLine();
                    writer.WriteLine($"            var other = ({entityDefinition.FullName})obj;");
                    writer.WriteLine();
                    writer.WriteLine($"            if (other == null)");
                    writer.WriteLine($"            {{");
                    writer.WriteLine($"                return false;");
                    writer.WriteLine($"            }}");

                    foreach (var property in entityDefinition.Properties.Where(p => !p.IsDerived))
                    {
                        writer.WriteLine();
                        writer.WriteLine($"            if ({property.NotEqualExpression})");
                        writer.WriteLine($"            {{");
                        writer.WriteLine($"                return false;");
                        writer.WriteLine($"            }}");
                    }

                    writer.WriteLine();
                    writer.WriteLine($"            return true;");
                    writer.WriteLine($"        }}");

                    foreach (var property in entityDefinition.Properties.Where(p => !p.IsDerived))
                    {
                        if (property.IsTypeProperty)
                        {
                            continue; // We handled this one above.
                        }

                        writer.WriteLine();
                        writer.WriteLine($"        public {property.PublicType} {property.PropertyName}");
                        writer.WriteLine($"        {{");
                        writer.WriteLine($"            get {{ return _{property.PropertyName}.Value; }}");

                        if (property.IsArray)
                        {
                            writer.WriteLine($"            set {{ _{property.PropertyName}.Set(value); }}");
                        }
                        else
                        {
                            writer.WriteLine($"            set {{ _{property.PropertyName}.Value = value; }}");
                        }

                        writer.WriteLine($"        }}");
                    }

                    writer.WriteLine($"    }}");
                    writer.WriteLine($"}}");
                }

                if (entityDefinitions.Count > 0)
                {
                    writer.WriteLine();
                    writer.WriteLine($"#endregion Entities");
                }

                // Generate the binder documents

                if (binderDefinitions.Count > 0)
                {
                    writer.WriteLine();
                    writer.WriteLine($"//-----------------------------------------------------------------------------");
                    writer.WriteLine();
                    writer.WriteLine($"#region Binder Documents");
                }

                foreach (var binderDefinition in binderDefinitions.Values.OrderBy(d => d.FullName, StringComparer.OrdinalIgnoreCase))
                {
                    writer.WriteLine();
                    writer.WriteLine($"namespace {binderDefinition.Namespace}");
                    writer.WriteLine($"{{");
                    writer.WriteLine($"    {binderDefinition.Visibility} partial class {binderDefinition.Name} : EntityDocument<{binderDefinition.TargetEntityType}>, IEntityDocument");
                    writer.WriteLine($"    {{");
                    writer.WriteLine($"        //---------------------------------------------------------------------");
                    writer.WriteLine($"        // Static members");
                    writer.WriteLine();
                    writer.WriteLine($"        internal static void _Register()");
                    writer.WriteLine($"        {{");
                    writer.WriteLine($"            EntityDatabase.Register<{binderDefinition.Name}>(");
                    writer.WriteLine($"                (document)                       => new {binderDefinition.Name}(document),");
                    writer.WriteLine($"                (properties, database, revision) => new {binderDefinition.Name}(properties, database, revision),");
                    writer.WriteLine($"                new string[]");
                    writer.WriteLine($"                {{");

                    foreach (var attachment in binderDefinition.Attachments.OrderBy(a => a.AttachmentName, StringComparer.OrdinalIgnoreCase))
                    {
                        writer.WriteLine($"                    \"{attachment.AttachmentName}\",");
                    }

                    writer.WriteLine($"                }});");
                    writer.WriteLine($"        }}");
                    writer.WriteLine();
                    writer.WriteLine($"        //---------------------------------------------------------------------");
                    writer.WriteLine($"        // Instance members");
                    writer.WriteLine();
                    writer.WriteLine($"        private {binderDefinition.Name}(Document document)");
                    writer.WriteLine($"            : base(document)");
                    writer.WriteLine($"        {{");

                    if (binderDefinition.Attachments.Count > 0)
                    {
                        writer.WriteLine($"            AttachmentEvent += OnAttachmentEvent;");
                    }

                    writer.WriteLine($"        }}");
                    writer.WriteLine();
                    writer.WriteLine($"        private {binderDefinition.Name}(IDictionary<string, object> properties, EntityDatabase database, Revision revision)");
                    writer.WriteLine($"            : base(properties, database, revision)");
                    writer.WriteLine($"        {{");

                    if (binderDefinition.Attachments.Count > 0)
                    {
                        writer.WriteLine($"            AttachmentEvent += OnAttachmentEvent;");
                    }

                    writer.WriteLine($"        }}");

                    if (binderDefinition.Attachments.Count == 1)
                    {
                        var attachment = binderDefinition.Attachments.First();

                        writer.WriteLine();
                        writer.WriteLine($"        private void OnAttachmentEvent(object sender, AttachmentEventArgs args)");
                        writer.WriteLine($"        {{");
                        writer.WriteLine($"            if (args.Name == \"{attachment.AttachmentName}\" && args.Path != {attachment.PropertyName})");
                        writer.WriteLine($"            {{");
                        writer.WriteLine($"                {attachment.PropertyName} = args.Path;");
                        writer.WriteLine();
                        writer.WriteLine($"                if (args.Notify)");
                        writer.WriteLine($"                {{");
                        writer.WriteLine($"                    OnPropertyChanged(\"{attachment.PropertyName}\");");
                        writer.WriteLine($"                }}");
                        writer.WriteLine($"            }}");
                        writer.WriteLine($"        }}");
                    }
                    else if (binderDefinition.Attachments.Count > 1)
                    {
                        writer.WriteLine();
                        writer.WriteLine($"        private void OnAttachmentEvent(object sender, AttachmentEventArgs args)");
                        writer.WriteLine($"        {{");
                        writer.WriteLine($"            switch (args.Name)");
                        writer.WriteLine($"            {{");

                        var isFirstCase = true;

                        foreach (var attachment in binderDefinition.Attachments.OrderBy(a => a.PropertyName, StringComparer.OrdinalIgnoreCase))
                        {
                            if (isFirstCase)
                            {
                                isFirstCase = false;
                            }
                            else
                            {
                                writer.WriteLine();
                            }

                            writer.WriteLine($"                case \"{attachment.AttachmentName}\":");
                            writer.WriteLine();
                            writer.WriteLine($"                    if (args.Path != {attachment.PropertyName})");
                            writer.WriteLine($"                    {{");
                            writer.WriteLine($"                        {attachment.PropertyName} = args.Path;");
                            writer.WriteLine();
                            writer.WriteLine($"                        if (args.Notify)");
                            writer.WriteLine($"                        {{");
                            writer.WriteLine($"                            OnPropertyChanged(\"{attachment.PropertyName}\");");
                            writer.WriteLine($"                        }}");
                            writer.WriteLine($"                    }}");
                            writer.WriteLine($"                    break;");
                        }

                        writer.WriteLine($"            }}");
                        writer.WriteLine($"        }}");
                    }

                    foreach (var attachment in binderDefinition.Attachments.OrderBy(a => a.PropertyName, StringComparer.OrdinalIgnoreCase))
                    {
                        writer.WriteLine();
                        writer.WriteLine($"        public string {attachment.PropertyName} {{ get; private set; }}");
                        writer.WriteLine();
                        writer.WriteLine($"        public void Set{attachment.PropertyName}(byte[] bytes, string contentType = null)");
                        writer.WriteLine($"        {{");
                        writer.WriteLine($"            base.SetAttachment(\"{attachment.AttachmentName}\", bytes, contentType);");
                        writer.WriteLine($"        }}");
                        writer.WriteLine();
                        writer.WriteLine($"        public void Set{attachment.PropertyName}(Stream input, string contentType = null)");
                        writer.WriteLine($"        {{");
                        writer.WriteLine($"            base.SetAttachment(\"{attachment.AttachmentName}\", input, contentType);");
                        writer.WriteLine($"        }}");
                        writer.WriteLine();
                        writer.WriteLine($"        public Attachment Get{attachment.PropertyName}()");
                        writer.WriteLine($"        {{");
                        writer.WriteLine($"            return base.GetAttachment(\"{attachment.AttachmentName}\");");
                        writer.WriteLine($"        }}");
                        writer.WriteLine();
                        writer.WriteLine($"        public void Remove{attachment.PropertyName}()");
                        writer.WriteLine($"        {{");
                        writer.WriteLine($"            base.RemoveAttachment(\"{attachment.AttachmentName}\");");
                        writer.WriteLine($"        }}");
                    }

                    writer.WriteLine($"    }}");
                    writer.WriteLine($"}}");
                }

                if (binderDefinitions.Count > 0)
                {
                    writer.WriteLine();
                    writer.WriteLine($"#endregion Binder Documents");
                }

                // Generate the included types.

                if (includedTypes.Count > 0)
                {
                    writer.WriteLine();
                    writer.WriteLine($"//-----------------------------------------------------------------------------");
                    writer.WriteLine();
                    writer.WriteLine($"#region Included Types");
                }

                foreach (var includedType in includedTypes.Values.OrderBy(it => it.FullName, StringComparer.OrdinalIgnoreCase))
                {
                    Log.LogMessage($"Including:    {GetTypeMapVisual(includedType.Type.FullName, includedType.FullName, maxSourceTypeLength)}");

                    writer.WriteLine();
                    writer.WriteLine($"namespace {includedType.Namespace}");
                    writer.WriteLine($"{{");

                    var originalType = includedType.Type;
                    var visibility   = includedType.IsInternal ? "internal" : "public";

                    if (originalType.GetTypeInfo().IsEnum)
                    {
                        var baseType = Enum.GetUnderlyingType(originalType);

                        writer.WriteLine($"    {visibility} enum {includedType.Name} : {baseType.FullName}");
                        writer.WriteLine($"    {{");

                        var values = Enum.GetValues(originalType);

                        for (int i = 0; i< values.Length; i++)
                        {
                            var value  = values.GetValue(i);
                            var ending = i < values.Length ? "," : string.Empty;

                            switch (Type.GetTypeCode(baseType))
                            {
                                case TypeCode.Byte:

                                    writer.WriteLine($"        {Enum.GetName(originalType, value)} = {(byte)value}{ending}");
                                    break;

                                case TypeCode.Int16:

                                    writer.WriteLine($"        {Enum.GetName(originalType, value)} = {(short)value}{ending}");
                                    break;

                                case TypeCode.Int32:

                                    writer.WriteLine($"        {Enum.GetName(originalType, value)} = {(int)value}{ending}");
                                    break;

                                case TypeCode.Int64:

                                    writer.WriteLine($"        {Enum.GetName(originalType, value)} = {(long)value}{ending}");
                                    break;

                                case TypeCode.SByte:

                                    writer.WriteLine($"        {Enum.GetName(originalType, value)} = {(sbyte)value}{ending}");
                                    break;

                                case TypeCode.UInt16:

                                    writer.WriteLine($"        {Enum.GetName(originalType, value)} = {(ushort)value}{ending}");
                                    break;

                                case TypeCode.UInt32:

                                    writer.WriteLine($"        {Enum.GetName(originalType, value)} = {(uint)value}{ending}");
                                    break;

                                case TypeCode.UInt64:

                                    writer.WriteLine($"        {Enum.GetName(originalType, value)} = {(ulong)value}{ending}");
                                    break;

                                default:

                                    Log.LogError($"Included enumeration [{originalType.FullName}] has the unsupported base type [{baseType.Name}].");
                                    goto exit;
                            }
                        }

                    exit:

                        writer.WriteLine($"    }}");
                    }
                    else
                    {
                        writer.WriteLine($"    {visibility} partial class {includedType.Name}");
                        writer.WriteLine($"    {{");

                        if (originalType.GetTypeInfo().BaseType != typeof(object))
                        {
                            Log.LogError($"Included class [{originalType.FullName}] inherits from an unsupported type.");
                        }

                        if (originalType.GetMembers(BindingFlags.DeclaredOnly).Count(m => (m.MemberType & (MemberTypes.Method  | MemberTypes.Property | MemberTypes.Event)) != 0) > 0)
                        {
                            Log.LogWarning($"Included class [{originalType.FullName}] has one or more methods, properties or events that won't be included in the generated code].");
                        }

                        foreach (var field in originalType.GetFields().Where(f => f.IsLiteral && !f.IsInitOnly))
                        {
                            object value = field.GetRawConstantValue();
                            string literal;

                            if (value == null)
                            {
                                literal = "null";
                            }
                            else
                            {
                                if (value is bool)
                                {
                                    literal = ((bool)value) ? "true" : "false";
                                }
                                else if (value is string)
                                {
                                    literal = $"\"{value}\"";
                                }
                                else
                                {
                                    literal = value.ToString();
                                }
                            } 

                            writer.WriteLine($"        public const {field.FieldType.Name} {field.Name} = {literal};");
                        }

                        writer.WriteLine($"    }}");
                    }

                    writer.WriteLine($"}}");
                }

                if (includedTypes.Count > 0)
                {
                    writer.WriteLine();
                    writer.WriteLine($"#endregion Included Types");
                }

                // Generate the static initialization class.

                Log.LogMessage($"Registration: {registerNamespace}.{registerClassName}");

                writer.WriteLine();
                writer.WriteLine($"//-----------------------------------------------------------------------------");
                writer.WriteLine();
                writer.WriteLine($"#region Registration");
                writer.WriteLine();
                writer.WriteLine($"namespace {registerNamespace}");
                writer.WriteLine($"{{");
                writer.WriteLine($"    public static class {registerClassName}");
                writer.WriteLine($"    {{");
                writer.WriteLine($"        public static void Register()");
                writer.WriteLine($"        {{");

                if (entityDefinitions.Count > 0)
                {
                    writer.WriteLine($"            // Entity registrations");
                    writer.WriteLine();
                    writer.WriteLine($"            var registrations = new List<EntityRegistration>(50);");
                    writer.WriteLine();

                    foreach (var entityDefinition in entityDefinitions.Values.OrderBy(d => d.FullName, StringComparer.OrdinalIgnoreCase))
                    {
                        writer.WriteLine($"            registrations.Add({entityDefinition.FullName}._GetRegistration());");
                    }

                    writer.WriteLine();
                    writer.WriteLine($"            Entity.Register(registrations);");
                }

                if (binderDefinitions.Count > 0)
                {
                    writer.WriteLine();
                    writer.WriteLine($"            // Binder (AKA derived document) registrations");
                    writer.WriteLine();

                    foreach (var binderDefinition in binderDefinitions.Values.OrderBy(d => d.FullName, StringComparer.OrdinalIgnoreCase))
                    {
                        writer.WriteLine($"            {binderDefinition.FullName}._Register();");
                    }
                }

                writer.WriteLine($"        }}");
                writer.WriteLine($"    }}");
                writer.WriteLine($"}}");
                writer.WriteLine();
                writer.WriteLine($"#endregion Registration");
                writer.WriteLine();

                Log.LogMessage($"Be sure to call [{registerNamespace}.{registerClassName}.Register()] during application initialization to register the entity types.");

                // Compare the generated source to the existing file (if one exists) and
                // overwrite the file if there's a difference.  This will improve build
                // performance and avoid burning SSDs with unncessary writes.

                var source      = writer.ToString();
                var sourceBytes = Encoding.UTF8.GetBytes(source);
                    
                if (!File.Exists(Output))
                {
                    File.WriteAllBytes(Output, sourceBytes);
                    Log.LogMessage($"Creating: {Output}");
                }
                else
                {
                    var existingSource = File.ReadAllText(Output, Encoding.UTF8);

                    if (source != existingSource)
                    {
                        File.WriteAllBytes(Output, sourceBytes);
                        Log.LogMessage($"Updating: {Output}");
                    }
                    else
                    {
                        Log.LogMessage($"Unchanged: {Output}");
                    }
                }
            }
            catch (Exception e)
            {
                Log.LogError($"{NeonHelper.ExceptionError(e)}");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Determines whether a <see cref="Type"/> is one of the non-entity types
        /// that are being included in the generated code.
        /// </summary>
        /// <param name="type">The original type.</param>
        /// <returns>The <see cref="IncludedType"/> for the included type or <c>null</c> if the type is not being included.</returns>
        public IncludedType GetIncludedType(Type type)
        {
            IncludedType info;

            if (includedTypes.TryGetValue(type, out info))
            {
                return info;
            }

            return null;
        }

        /// <summary>
        /// Returns a string that visualizes the type generation.
        /// </summary>
        /// <param name="sourceTypeName">The source type name.</param>
        /// <param name="targetTypeName">The target type name.</param>
        /// <param name="sourceWidth">The width of the maximum source type name.</param>
        /// <returns>The visual string.</returns>
        private string GetTypeMapVisual(string sourceTypeName, string targetTypeName, int sourceWidth)
        {
            return $"{PadRight(sourceTypeName, sourceWidth)}--> {targetTypeName}";
        }

        /// <summary>
        /// Pads a string with enough spaces on the right such that the result string
        /// has the specified width.
        /// </summary>
        /// <param name="input">The input string.</param>
        /// <param name="width">The desired width.</param>
        /// <returns>The padded string.</returns>
        private string PadRight(string input, int width)
        {
            if (input.Length >= width)
            {
                return input;
            }
            else
            {
                return input + new string(' ', width - input.Length);
            }
        }

        /// <summary>
        /// Looks for entity information based on its type name.
        /// </summary>
        /// <param name="entityDefinitionName">The fully qualified definition interface name.</param>
        /// <returns>The entity information if found; <c>null</c> otherwise.</returns>
        public EntityInfo FindEntity(string entityDefinitionName)
        {
            EntityInfo entityInfo;

            if (entityDefinitions.TryGetValue(entityDefinitionName, out entityInfo))
            {
                return entityInfo;
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Looks for binder document information based on its type name.
        /// </summary>
        /// <param name="binderDefinitionName">The fully qualified definition interface name.</param>
        /// <returns>The binder information if found; <c>null</c> otherwise.</returns>
        public BinderInfo FindBinder(string binderDefinitionName)
        {
            BinderInfo binderInfo;

            if (binderDefinitions.TryGetValue(binderDefinitionName, out binderInfo))
            {
                return binderInfo;
            }
            else
            {
                return null;
            }
        }
    }
}
