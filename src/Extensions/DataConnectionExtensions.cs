using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using LinqToDB;
using LinqToDB.Data;
using LinqToDB.Mapping;
using LinqToDB.SqlQuery;

namespace Linq2Db.SqlServer.ChangeTracking
{
    public static class DataConnectionExtensions
    {
        private static readonly MethodInfo _ctSyncMethod;
        private static readonly MethodInfo _ctAsyncMethod;

        static DataConnectionExtensions()
        {
            _ctSyncMethod = typeof(DataConnectionExtensions).GetMethod(nameof(GetChangesEnumerable), 
                BindingFlags.NonPublic | BindingFlags.Static);
            _ctAsyncMethod = typeof(DataConnectionExtensions).GetMethod(nameof(GetChangesEnumerableAsync), 
                BindingFlags.NonPublic | BindingFlags.Static);
        }
        
        [PublicAPI]
        public static bool IsSqlServer(this DataConnection ctx)
        {
            switch (ctx.DataProvider.Name)
            {
                case ProviderName.SqlServer:
                case ProviderName.SqlServer2000:
                case ProviderName.SqlServer2008:
                case ProviderName.SqlServer2012:
                case ProviderName.SqlServer2014:
                   
                    return true;
                default:
                    return false;
            }
        }

        [PublicAPI]
        public static bool IsCtCompatible(this DataConnection ctx)
        {
            switch (ctx.DataProvider.Name)
            {
                case ProviderName.SqlServer:
                case ProviderName.SqlServer2008:
                case ProviderName.SqlServer2012:
                case ProviderName.SqlServer2014:
                    return true;
                default:
                    return false;
            }
        }

        
        private static void CheckCtCompatible(this DataConnection ctx)
        {
            if(!ctx.IsSqlServer())
                throw new ArgumentException("Provider is not SqlServer - not compatible with change tracking");
            if(!ctx.IsCtCompatible())
                throw new ArgumentException("SqlServer version is not compatible with change tracking");
        }
        
        [PublicAPI]
        public static string GetDatabaseName(this DataConnection ctx)
        {
            ctx.CheckCtCompatible();
            string connectionString;
            switch (ctx)
            {
                case var _ when ctx.Transaction != null:
                    connectionString = ctx.Transaction.Connection.ConnectionString;
                    break;
                case var _ when ctx.Connection != null:
                    connectionString = ctx.Connection.ConnectionString;
                    break;
                default:
                    connectionString = ctx.ConnectionString;
                    break;
            }
            return new SqlConnectionStringBuilder(connectionString).InitialCatalog;
        }


        [PublicAPI]
        public static void EnableChangeTracking(this DataConnection ctx, uint retentionPeriod, RetentionMeasure measure,
            bool autoCleanup = true) 
            => ctx.Execute(Sql.DbEnableChangeTracking(ctx.GetDatabaseName(), retentionPeriod, measure, autoCleanup));

        [PublicAPI]
        public static Task EnableChangeTrackingAsync(this DataConnection ctx, uint retentionPeriod,
            RetentionMeasure measure,
            bool autoCleanup = true, CancellationToken token = default)
            => ctx.ExecuteAsync(
                Sql.DbEnableChangeTracking(ctx.GetDatabaseName(), retentionPeriod, measure, autoCleanup), token);

        [PublicAPI]
        public static void DisableChangeTracking(this DataConnection ctx)
            => ctx.Execute(Sql.DbDisableChangeTracking(ctx.GetDatabaseName()));
        
        [PublicAPI]
        public static Task DisableChangeTrackingAsync(this DataConnection ctx, CancellationToken token = default)
            => ctx.ExecuteAsync(Sql.DbDisableChangeTracking(ctx.GetDatabaseName()), token);

        [PublicAPI]
        public static void EnableChangeTracking<T>(this DataConnection ctx, bool trackColumnUpdate = false)
        {
            ctx.CheckCtCompatible();
            var descriptor = ctx.MappingSchema.GetEntityDescriptor(typeof(T));
            if (descriptor.InheritanceMapping.Count > 0)
                throw new ArgumentException($"Cannot change track entities with inheritance!");
            ctx.Execute(Sql.TableEnableChangeTracking(descriptor.SchemaName, descriptor.TableName, trackColumnUpdate));

        }

        [PublicAPI]
        public static Task EnableChangeTrackingAsync<T>(this DataConnection ctx, bool trackColumnUpdate = false,
            CancellationToken token = default)
        {
            ctx.CheckCtCompatible();
            var descriptor = ctx.MappingSchema.GetEntityDescriptor(typeof(T));
            if (descriptor.InheritanceMapping.Count > 0)
                throw new ArgumentException($"Cannot change track entities with inheritance!");
            return ctx.ExecuteAsync(
                Sql.TableEnableChangeTracking(descriptor.SchemaName, descriptor.TableName, trackColumnUpdate), token);
        }

        [PublicAPI]
        public static void DisableChangeTracking<T>(this DataConnection ctx)
        {
            ctx.CheckCtCompatible();
            var descriptor = ctx.MappingSchema.GetEntityDescriptor(typeof(T));
            ctx.Execute(Sql.TableDisableChangeTracking(descriptor.SchemaName, descriptor.TableName));
        }
        
        [PublicAPI]
        public static Task DisableChangeTrackingAsync<T>(this DataConnection ctx, CancellationToken token = default)
        {
            ctx.CheckCtCompatible();
            var descriptor = ctx.MappingSchema.GetEntityDescriptor(typeof(T));
            return ctx.ExecuteAsync(Sql.TableDisableChangeTracking(descriptor.SchemaName, descriptor.TableName), token);
        }

        public static IEnumerable<Changed<T>> GetChanges<T>(this DataConnection ctx, long version) 
            where T : new()
        {
            ctx.CheckCtCompatible();
            var descriptor = ctx.MappingSchema.GetEntityDescriptor(typeof(T));
            var (ctType, ex, map) = descriptor.GetCtTypeForEntity();
            var method = _ctSyncMethod.MakeGenericMethod(typeof(T), ctType);
            var query = (IEnumerable<Changed<T>>) method.Invoke(null,
                new object[] {ctx, ex, map, version});
            return query;

        }
        
        public static Task<IEnumerable<Changed<T>>> GetChangesAsync<T>(this DataConnection ctx, long version, 
            CancellationToken token = default) 
            where T : new()
        {
            ctx.CheckCtCompatible();
            var descriptor = ctx.MappingSchema.GetEntityDescriptor(typeof(T));
            var (ctType, ex, map) = descriptor.GetCtTypeForEntity();
            var method = _ctAsyncMethod.MakeGenericMethod(typeof(T), ctType);
            var query = (Task<IEnumerable<Changed<T>>>) method.Invoke(null,
                new object[] {ctx, ex, map, version, token});
            return query;

        }
        
        private static async Task<IEnumerable<Changed<T>>> GetChangesEnumerableAsync<T, TCt>(DataConnection ctx,
            Expression<Func<TCt, T, bool>> joiner, Func<TCt, T> mapping, long version, CancellationToken token) 
            where T : class, new() 
            where TCt : CtBase
        {
            var lst = await GetChangesQuery(ctx, joiner, mapping, version)
                .ToListAsync(token);
                
            return lst.Select(p => new Changed<T>(p.ChangeType, p.Version,
                    p.ChangeType == ChangeType.Delete ? mapping(p.Ct) : p.Entity));
        }

        private static IEnumerable<Changed<T>> GetChangesEnumerable<T, TCt>(DataConnection ctx,
            Expression<Func<TCt, T, bool>> joiner, Func<TCt, T> mapping, long version) 
            where T : class, new() 
            where TCt : CtBase
        {
            return GetChangesQuery(ctx, joiner, mapping, version)
                .ToList()
                .Select(p => new Changed<T>(p.ChangeType, p.Version,
                    p.ChangeType == ChangeType.Delete ? mapping(p.Ct) : p.Entity));
        }

        private static IQueryable<Changed<T, TCt>> GetChangesQuery<T, TCt>(DataConnection ctx, 
            Expression<Func<TCt,T , bool>> joiner, Func<TCt, T> mapping, long version)
            where TCt : CtBase
            where T : class, new()
        {
            var descriptor = ctx.MappingSchema.GetEntityDescriptor(typeof(T));
            var pkString = string.Join(", ", descriptor.Columns.Where(p => p.IsPrimaryKey).Select(p => p.ColumnName));
            var schema = string.IsNullOrWhiteSpace(descriptor.SchemaName) ? "dbo" : descriptor.SchemaName; 
            var sql = $@"SELECT *  FROM CHANGETABLE(CHANGES [{schema}].[{descriptor.TableName}], @p0) ct";
            var queryChanges = ctx.FromSql<TCt>(sql, new DataParameter("p", version));
            var query = queryChanges
                .LeftJoin(ctx.GetTable<T>(), joiner, (c, e) => new {Change = c, Entity = e})
                .Select(p =>
                    new Changed<T, TCt>(p.Change.SYS_CHANGE_OPERATION, p.Change.SYS_CHANGE_VERSION, p.Change,
                        p.Entity));
            return query;
        }

        [PublicAPI]
        public static long GetChangeTrackingVersion(this DataConnection ctx)
            => ctx.Execute<long>(Sql.ChangeTrackingVersion);
        
        [PublicAPI]
        public static Task<long> GetChangeTrackingVersionAsync(this DataConnection ctx, CancellationToken token = default)
            => ctx.ExecuteAsync<long>(Sql.ChangeTrackingVersion, token);

    }
    
    
}