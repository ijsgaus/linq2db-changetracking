using LinqToDB.Mapping;

namespace Linq2Db.SqlServer
{
    /// <summary>
    /// Type of entity change
    /// </summary>
    public enum ChangeType
    {
        /// <summary>
        /// Entity inserted
        /// </summary>
        [MapValue("I")]
        Insert,

        /// <summary>
        /// Entity updated
        /// </summary>
        [MapValue("U")]
        Update,

        /// <summary>
        /// Entity deleted
        /// </summary>
        [MapValue("D")]
        Delete
    }
}