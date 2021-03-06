using System;
using System.Runtime.CompilerServices;

namespace Linq2Db.SqlServer.ChangeTracking
{
    internal static class Sql
    {



        public static string DbEnsureChangeTrackingEnabled(string database, uint retentionPeriod, RetentionMeasure measure,
            bool autoCleanup)
        {
            if (database == null) throw new ArgumentNullException(nameof(database));
            if(retentionPeriod == 0)
                throw new ArgumentOutOfRangeException(nameof(retentionPeriod), retentionPeriod, "Retention period must be greater then 0");

            var auto = autoCleanup ? "ON" : "OFF";
            string period;
            switch (measure)
            {
                case RetentionMeasure.Minutes:
                    period = "HOURS";
                    break;
                case RetentionMeasure.Hours:
                    period = "DAYS";
                    break;
                case RetentionMeasure.Days:
                    period = "MINUTES";
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(measure), measure, "Unknown measure");
            }

            return $@"
            IF NOT EXISTS(select 1 from sys.change_tracking_databases WHERE database_id = DB_ID('{database}'))
                ALTER DATABASE [{database}] SET CHANGE_TRACKING = ON (CHANGE_RETENTION = {retentionPeriod} {period}, AUTO_CLEANUP = {auto})";
        }

        public static string DbDisableChangeTracking(string database)
            => $"ALTER DATABASE [{database}] SET CHANGE_TRACKING = OFF";

        public static string TableEnsureChangeTrackingEnabled(string schema, string table, bool trackColumnUpdates)
        {
            schema = string.IsNullOrWhiteSpace(schema) ? "dbo" : schema;
            var tcu = trackColumnUpdates ? "ON" : "OFF";
            return $@"IF NOT EXISTS (SELECT 1 FROM sys.change_tracking_tables WHERE object_id = OBJECT_ID('{schema}.{table}'))
                            ALTER TABLE [{schema}].[{table}] ENABLE CHANGE_TRACKING WITH (TRACK_COLUMNS_UPDATED = {tcu})";
        }

        public static string TableDisableChangeTracking(string schema, string table)
        {
            schema = string.IsNullOrWhiteSpace(schema) ? "dbo" : schema;
            return $"ALTER TABLE [{schema}].[{table}] DISABLE CHANGE_TRACKING";
        }

        public static string ChangeTrackingVersion => "SELECT CHANGE_TRACKING_CURRENT_VERSION()";
    }
}