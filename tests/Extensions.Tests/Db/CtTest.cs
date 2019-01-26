using LinqToDB.Mapping;

namespace Extensions.Tests.Db
{
    [Table]
    public class CtTest
    {
        [Column, PrimaryKey, Identity]
        public int Id { get; set; }
        
        [Column, NotNull]
        public string Name { get; set; }
    }
}