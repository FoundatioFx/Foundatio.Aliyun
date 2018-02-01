using System;

namespace Foundatio.Storage {
    public class AliyunFileStorageOptions : SharedOptions {
        public string ConnectionString { get; set; }
    }

    public class AliyunFileStorageOptionsBuilder : SharedOptionsBuilder<AliyunFileStorageOptions, AliyunFileStorageOptionsBuilder> {
        public AliyunFileStorageOptionsBuilder ConnectionString(string connectionString) {
            if (String.IsNullOrEmpty(connectionString))
                throw new ArgumentNullException(nameof(connectionString));
            Target.ConnectionString = connectionString;
            return this;
        }
    }
}