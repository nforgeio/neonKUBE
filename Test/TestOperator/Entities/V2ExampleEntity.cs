using k8s;
using k8s.Models;

using Neon.Operator.Attributes;

namespace TestOperator
{
    /// <summary>
    /// V2ExampleEntity
    /// </summary>
    [KubernetesEntity(Group = KubeGroup, Kind = KubeKind, ApiVersion = KubeApiVersion, PluralName = KubePlural)]
    [EntityVersion(Served = true, Storage = true)]
    public class V2ExampleEntity : IKubernetesObject<V1ObjectMeta>, ISpec<V2ExampleEntity.V1ExampleSpec>, IStatus<V2ExampleEntity.V1ExampleStatus>
    {
        /// <summary>
        /// The API version this Kubernetes type belongs to.
        /// </summary>
        public const string KubeApiVersion = "v2";

        /// <summary>
        /// The Kubernetes named schema this object is based on.
        /// </summary>
        public const string KubeKind = "Example";

        /// <summary>
        /// The Group this Kubernetes type belongs to.
        /// </summary>
        public const string KubeGroup = "example.neonkube.io";

        /// <summary>
        /// The plural name of the entity.
        /// </summary>
        public const string KubePlural = "examples";

        /// <summary>
        /// Constructor.
        /// </summary>
        public V2ExampleEntity()
        {
            ApiVersion = $"{KubeGroup}/{KubeApiVersion}";
            Kind = KubeKind;
        }

        /// <inheritdoc/>
        public string ApiVersion { get; set; }
        /// <inheritdoc/>
        public string Kind { get; set; }
        /// <inheritdoc/>
        public V1ObjectMeta Metadata { get; set; }
        /// <inheritdoc/>
        public V1ExampleSpec Spec { get; set; }
        /// <inheritdoc/>
        public V1ExampleStatus Status { get; set; }

        /// <summary>
        /// V1ExampleSpec
        /// </summary>
        public class V1ExampleSpec
        {
            public string Message { get; set; }
        }

        /// <summary>
        /// V1ExampleStatus
        /// </summary>
        public class V1ExampleStatus
        {
            public string Message { get; set; }
        }
    }
}
