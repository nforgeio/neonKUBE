using k8s;
using k8s.Models;

namespace OperatorTemplate
{
    [KubernetesEntity(Group = KubeGroup, Kind = KubeKind, ApiVersion = KubeApiVersion, PluralName = KubePlural)]
    public class V1ExampleEntity : IKubernetesObject<V1ObjectMeta>, ISpec<V1ExampleEntity.V1ExampleSpec>, IStatus<V1ExampleEntity.V1ExampleStatus>
    {
        /// <summary>
        /// The API version this Kubernetes type belongs to.
        /// </summary>
        public const string KubeApiVersion = "v1alpha1";

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
        public V1ExampleEntity()
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

        public class V1ExampleSpec
        {
            public string Message { get; set; }
        }

        public class V1ExampleStatus
        {
            public string Message { get; set; }
        }
    }
}
