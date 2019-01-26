using LinqToDB.Mapping;

namespace Linq2Db.SqlServer
{
    public enum ChangeType
    {
        [MapValue("I")]
        Insert,
        [MapValue("U")]
        Update,
        [MapValue("D")]
        Delete
    }
}