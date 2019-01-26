using System.Diagnostics.CodeAnalysis;

namespace Linq2Db.SqlServer
{
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public class CtBase
    {
        public ChangeType SYS_CHANGE_OPERATION { get; set; } 
        public long SYS_CHANGE_VERSION { get; set; }
    }
}