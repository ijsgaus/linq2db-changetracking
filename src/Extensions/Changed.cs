namespace Linq2Db.SqlServer
{
    /// <summary>
    /// Envelope for describe changed entity
    /// </summary>
    /// <typeparam name="T">entity type</typeparam>
    public class Changed<T> where T : class, new()
    {
        /// <summary>
        /// create changed entity descriptor
        /// </summary>
        /// <param name="changeType">type of entity change</param>
        /// <param name="version">version of change tracking when changed</param>
        /// <param name="entity">entity with filled key properties or full entity</param>
        public Changed(ChangeType changeType, long version, T entity)
        {
            ChangeType = changeType;
            Version = version;
            Entity = entity;
        }

        /// <summary>
        /// type of entity change
        /// </summary>
        public ChangeType ChangeType { get; }

        /// <summary>
        /// entity change version
        /// </summary>
        public long Version { get; }

        /// <summary>
        /// Is entity loaded or key only
        /// </summary>
        public bool IsFullLoaded => this.ChangeType != ChangeType.Delete;

        /// <summary>
        /// Current entity state
        /// </summary>
        public T Entity { get; }
    }

    internal class Changed<T, TCt> where T : class, new()
    {
        public TCt Ct { get; set; }
        public T Entity { get; set; }
    }
}