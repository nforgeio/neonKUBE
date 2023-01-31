using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Neon.Kube.Operator.ResourceManager
{
    /// <inheritdoc/>
    public class EntityFilter<TController, TEntity> : IEntityFilter<TController, TEntity> 
    {
        /// <inheritdoc/>
        public Func<TEntity, bool> Filter { get; set; } = new Func<TEntity, bool>(resource => true);
    }
}