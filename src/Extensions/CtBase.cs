using System.Diagnostics.CodeAnalysis;

namespace Linq2Db.SqlServer
{
    /// <summary>
    /// Base class for code generated change table entity
    /// </summary>
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public class CtBase
    {
        /// <summary>
        /// Change type
        /// </summary>
        public ChangeType SYS_CHANGE_OPERATION { get; set; }

        /// <summary>
        /// Change version
        /// </summary>
        public long SYS_CHANGE_VERSION { get; set; }
    }
}