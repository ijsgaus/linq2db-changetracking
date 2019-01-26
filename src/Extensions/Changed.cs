namespace Linq2Db.SqlServer
{
    public class Changed<T>
    {
        public Changed(ChangeType changeType, long version, T entity)
        {
            ChangeType = changeType;
            Version = version;
            Entity = entity;
        }

        public ChangeType ChangeType { get; }
        public long Version { get; }
        public bool IsFullLoaded => this.ChangeType != ChangeType.Delete;
        public T Entity { get; }
    }
    
    public class Changed<T, TCt>
    {
        public Changed(ChangeType changeType, long version, TCt ct, T entity)
        {
            ChangeType = changeType;
            Version = version;
            Ct = ct;
            Entity = entity;
        }

        public ChangeType ChangeType { get; }
        public long Version { get; }
        public TCt Ct { get; }
        public bool IsFullLoaded => this.ChangeType != ChangeType.Delete;
        public T Entity { get; }
    }
}