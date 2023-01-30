using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Net.NetworkInformation;
using System.Text;
using k8s;
using k8s.Models;

using Neon.Kube.Operator;

namespace OperatorTemplate
{
    /// <summary>
    /// Used for unit testing Kubernetes clients.
    /// </summary>
    [KubernetesEntity(Group = KubeGroup, ApiVersion = KubeApiVersion, Kind = KubeKind, PluralName = KubePlural)]
    [EntityScope(EntityScope.Cluster)]
    public class V1ExampleEntity : IKubernetesObject<V1ObjectMeta>, ISpec<ExampleSpec>, IStatus<ExampleStatus>, IValidate
    {
        /// <summary>
        /// Object API group.
        /// </summary>
        public const string KubeGroup = "example.neonkube.io";

        /// <summary>
        /// Object API version.
        /// </summary>
        public const string KubeApiVersion = "v1alpha1";

        /// <summary>
        /// Object API kind.
        /// </summary>
        public const string KubeKind = "NeonExampleObject";

        /// <summary>
        /// Object plural name.
        /// </summary>
        public const string KubePlural = "neonexampleobjects";

        /// <summary>
        /// Default constructor.
        /// </summary>
        public V1ExampleEntity()
        {
            ApiVersion = $"{KubeGroup}/{KubeApiVersion}";
            Kind = KubeKind;
        }

        /// <summary>
        /// Gets or sets APIVersion defines the versioned schema of this
        /// representation of an object. Servers should convert recognized
        /// schemas to the latest internal value, and may reject unrecognized
        /// values. More info:
        /// https://git.k8s.io/community/contributors/devel/sig-architecture/api-conventions.md#resources
        /// </summary>
        public string ApiVersion { get; set; }

        /// <summary>
        /// Gets or sets kind is a string value representing the REST resource
        /// this object represents. Servers may infer this from the endpoint
        /// the client submits requests to. Cannot be updated. In CamelCase.
        /// More info:
        /// https://git.k8s.io/community/contributors/devel/sig-architecture/api-conventions.md#types-kinds
        /// </summary>
        public string Kind { get; set; }

        /// <summary>
        /// Gets or sets standard object metadata.
        /// </summary>
        public V1ObjectMeta Metadata { get; set; }

        /// <summary>
        /// Gets or sets specification of the desired behavior of the
        /// Tenant.
        /// </summary>
        public ExampleSpec Spec { get; set; }

        /// <summary>
        /// Gets or sets specification of the desired behavior of the
        /// Tenant.
        /// </summary>
        public ExampleStatus Status { get; set; }

        /// <summary>
        /// Validate the object.
        /// </summary>
        /// <exception cref="ValidationException">Thrown if validation fails.</exception>
        public virtual void Validate()
        {
        }
    }

    /// <summary>
    /// The node execute task specification.
    /// </summary>
    public class ExampleSpec
    {
        /// <summary>
        /// A test string.
        /// </summary>
        public string Message { get; set; }
    }

    /// <summary>
    /// The node execute task specification.
    /// </summary>
    public class ExampleStatus
    {
        /// <summary>
        /// A test string.
        /// </summary>
        public string Message { get; set; }
    }
}
