using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Neon.Kube.Operator.ResourceManager
{
    /// <summary>
    /// Allows filtering entities for a given controller type.
    /// </summary>
    /// <typeparam name="TController"></typeparam>
    /// <typeparam name="TEntity"></typeparam>
    public interface IEntityFilter<TController, TEntity>
    {
        /// <summary>
        /// The Filter.
        /// </summary>
        public Func<TEntity, bool> Filter { get; set; }
    }
}