using System;
using Extensions.Tests.Db;
using Linq2Db.SqlServer.ChangeTracking;
using LinqToDB;
using LinqToDB.Data;
using Xunit;

namespace Extensions.Tests
{
    public class NoDatabaseTests
    {
        [Fact]
        public void ShouldDetermineSqlServer()
        {
            using (var conn = new DataConnection(ProviderName.SqlServer, ""))
            {
                Assert.True(conn.IsSqlServer());
            }
            using(var conn = new DataConnection(ProviderName.SqlCe, ""))
            {
                Assert.False(conn.IsSqlServer());
            }
        }

        [Fact]
        public void ShouldCreateCtType()
        {
            using (var conn = new DataConnection(ProviderName.SqlServer, ""))
            {
                var (type, _, _) = conn.MappingSchema.GetEntityDescriptor(typeof(CtTest)).GetCtTypeForEntity();
                Assert.NotNull(type.GetProperty("Id"));
                var (t1 , _, _)= conn.MappingSchema.GetEntityDescriptor(typeof(CtTest)).GetCtTypeForEntity();
                Assert.Equal(type, t1);
            }
        }
        
        
    }
}