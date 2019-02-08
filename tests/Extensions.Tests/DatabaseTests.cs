using System;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Extensions.Tests.Db;
using Linq2Db.SqlServer;
using Linq2Db.SqlServer.ChangeTracking;
using LinqToDB;
using LinqToDB.Data;
using Xunit;

namespace Extensions.Tests
{
    public class DatabaseTests : IDisposable
    {
        private readonly string _masterCs;
        private readonly string _dbCs;

        public DatabaseTests()
        {
            var builder = new SqlConnectionStringBuilder();
            builder.DataSource = "localhost";
            builder.InitialCatalog = "master";
            builder.UserID = "sa";
            builder.Password = "rt11sjkbrfynhjg";
            _masterCs = builder.ToString();
            using (var masterCon = new DataConnection(ProviderName.SqlServer2014, _masterCs))
            {
                masterCon.Execute("CREATE DATABASE Linq2dbTests");
            }

            builder.InitialCatalog = "Linq2dbTests";
            _dbCs = builder.ToString();


            using(var con = new TestDb(ProviderName.SqlServer2014,_dbCs))
                con.CreateTable<CtTest>();
        }

        private TestDb CreateDb()
         => new TestDb(ProviderName.SqlServer2014, _dbCs);

        [Fact]
        public void EnableDisableCt()
        {
            using (var db = CreateDb())
            {
                db.EnsureChangeTrackingEnabled(10, RetentionMeasure.Minutes);
                db.EnsureChangeTrackingEnabled(10, RetentionMeasure.Minutes);
                db.DisableChangeTracking();
            }
        }

        [Fact]
        public void EnableDisableCtOnTable()
        {
            using (var db = CreateDb())
            {
                db.EnsureChangeTrackingEnabled(10, RetentionMeasure.Minutes);
                db.EnsureChangeTrackingEnabled<CtTest>();
                db.EnsureChangeTrackingEnabled<CtTest>();
                db.DisableChangeTracking<CtTest>();
                db.DisableChangeTracking();
            }
        }

        [Fact]
        public void ShouldGetCtChanges()
        {
            using (var db = CreateDb())
            {
                db.EnsureChangeTrackingEnabled(10, RetentionMeasure.Minutes);
                db.EnsureChangeTrackingEnabled<CtTest>();
                db.CtTests.Insert(() => new CtTest {Name = "123"});
                var fst = db.GetChanges<CtTest>(0, false).FirstOrDefault();
                Assert.NotNull(fst);
                Assert.Equal(ChangeType.Insert, fst.ChangeType);
                Assert.Equal("123", fst.Entity.Name);
                Assert.Equal(1, fst.Entity.Id);

                fst = db.GetChanges<CtTest>(0).FirstOrDefault();

                Assert.NotNull(fst);
                Assert.Equal(ChangeType.Insert, fst.ChangeType);
                Assert.Null(fst.Entity.Name);
                Assert.Equal(1, fst.Entity.Id);

                var version = db.GetChangeTrackingVersion();
                db.CtTests.Where(p => p.Id == 1).Delete();
                var snd = db.GetChanges<CtTest>(version, false).FirstOrDefault();
                Assert.NotNull(snd);
                Assert.Equal(ChangeType.Delete, snd.ChangeType);
                Assert.Equal(1, snd.Entity.Id);
                db.DisableChangeTracking<CtTest>();
                db.DisableChangeTracking();
            }
        }

        [Fact]
        public async Task ShouldGetCtChangesAsync()
        {
            using (var db = CreateDb())
            {
                db.EnsureChangeTrackingEnabled(10, RetentionMeasure.Minutes);
                db.EnsureChangeTrackingEnabled<CtTest>();
                await db.CtTests.InsertAsync(() => new CtTest {Name = "123"});
                var fst = (await db.GetChangesAsync<CtTest>(0, false)).FirstOrDefault();
                Assert.NotNull(fst);
                Assert.Equal(ChangeType.Insert, fst.ChangeType);
                Assert.Equal("123", fst.Entity.Name);
                Assert.Equal(1, fst.Entity.Id);
                var version = await db.GetChangeTrackingVersionAsync();
                await db.CtTests.Where(p => p.Id == 1).DeleteAsync();
                var snd = (await db.GetChangesAsync<CtTest>(version, false)).FirstOrDefault();
                Assert.NotNull(snd);
                Assert.Equal(ChangeType.Delete, snd.ChangeType);
                Assert.Equal(1, snd.Entity.Id);
                await db.DisableChangeTrackingAsync<CtTest>();
                await db.DisableChangeTrackingAsync();
            }
        }

        public void Dispose()
        {
            using (var con = new DataConnection(ProviderName.SqlServer2014, _masterCs))
                con.Execute("ALTER DATABASE Linq2dbTests SET SINGLE_USER WITH ROLLBACK IMMEDIATE;DROP DATABASE Linq2dbTests;");
        }
    }
}