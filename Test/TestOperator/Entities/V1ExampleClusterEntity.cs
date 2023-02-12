using k8s;
using k8s.Models;
using Neon.Kube.Resources;

namespace TestOperator
{
    /// <summary>
    /// V1ExampleClusterEntity
    /// </summary>
    [KubernetesEntity(Group = KubeGroup, Kind = KubeKind, ApiVersion = KubeApiVersion, PluralName = KubePlural)]
    [EntityScope(EntityScope.Cluster)]
    public class V1ExampleClusterEntity : IKubernetesObject<V1ObjectMeta>, ISpec<V1ExampleClusterEntity.V1ExampleSpec>, IStatus<V1ExampleClusterEntity.V1ExampleStatus>
    {
        /// <summary>
        /// The API version this Kubernetes type belongs to.
        /// </summary>
        public const string KubeApiVersion = "v1alpha1";

        /// <summary>
        /// The Kubernetes named schema this object is based on.
        /// </summary>
        public const string KubeKind = "ExampleCluster";

        /// <summary>
        /// The Group this Kubernetes type belongs to.
        /// </summary>
        public const string KubeGroup = "example.neonkube.io";

        /// <summary>
        /// The plural name of the entity.
        /// </summary>
        public const string KubePlural = "exampleclusters";

        /// <summary>
        /// Constructor.
        /// </summary>
        public V1ExampleClusterEntity()
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
