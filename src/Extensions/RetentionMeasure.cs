namespace Linq2Db.SqlServer.ChangeTracking
{
    /// <summary>
    /// Cahnge tracking retention measure item
    /// </summary>
    public enum RetentionMeasure
    {
        /// <summary>
        /// in minutes
        /// </summary>
        Minutes,
        /// <summary>
        /// in hours
        /// </summary>
        Hours,
        /// <summary>
        /// in days
        /// </summary>
        Days
    }
}