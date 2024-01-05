using System;

namespace Foundatio.Storage
{
    public class AliyunFileStorageConnectionStringBuilder : AliyunConnectionStringBuilder
    {
        private string _bucket;

        public AliyunFileStorageConnectionStringBuilder()
        {
        }

        public AliyunFileStorageConnectionStringBuilder(string connectionString) : base(connectionString)
        {
        }

        public string Bucket
        {
            get => String.IsNullOrEmpty(_bucket) ? "storage" : _bucket;
            set => _bucket = value;
        }

        protected override bool ParseItem(string key, string value)
        {
            if (String.Equals(key, "Bucket", StringComparison.OrdinalIgnoreCase))
            {
                Bucket = value;
                return true;
            }
            return base.ParseItem(key, value);
        }

        public override string ToString()
        {
            string connectionString = base.ToString();
            if (!String.IsNullOrEmpty(_bucket))
                connectionString += "Bucket=" + Bucket + ";";
            return connectionString;
        }
    }
}
