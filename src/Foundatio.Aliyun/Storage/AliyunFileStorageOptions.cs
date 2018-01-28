using System;

namespace Foundatio.Storage {
    public class AliyunFileStorageOptions : FileStorageOptionsBase {
        public string ConnectionString { get; set; }
    }

    public static class AliyunFileStorageOptionsExtensions {
        public static IOptionsBuilder<AliyunFileStorageOptions> ConnectionString(this IOptionsBuilder<AliyunFileStorageOptions> options, string connectionString) {
            if (options == null)
                throw new ArgumentNullException(nameof(options));
            if (string.IsNullOrEmpty(connectionString))
                throw new ArgumentNullException(nameof(connectionString));
            options.Target.ConnectionString = connectionString;
            return options;
        }
    }
}