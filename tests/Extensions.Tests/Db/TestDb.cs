using LinqToDB;
using LinqToDB.Data;
using LinqToDB.Mapping;

namespace Extensions.Tests.Db
{
    public class TestDb : DataConnection
    {
        public TestDb([JetBrains.Annotations.NotNull] string providerName, [JetBrains.Annotations.NotNull] string connectionString) : base(providerName, connectionString)
        {
        }

        public ITable<CtTest> CtTests => GetTable<CtTest>();

    }
}