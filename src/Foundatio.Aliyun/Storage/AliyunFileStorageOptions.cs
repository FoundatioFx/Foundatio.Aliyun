using System;

namespace Foundatio.Storage {
    public class AliyunFileStorageOptions : FileStorageOptionsBase {
        public string ConnectionString { get; set; }
    }

    public static class AliyunFileStorageOptionsExtensions {
        public static IOptionsBuilder<AliyunFileStorageOptions> ConnectionString(this IOptionsBuilder<AliyunFileStorageOptions> builder, string connectionString) {
            if (builder == null)
                throw new ArgumentNullException(nameof(builder));
            if (string.IsNullOrEmpty(connectionString))
                throw new ArgumentNullException(nameof(connectionString));
            builder.Target.ConnectionString = connectionString;
            return builder;
        }
    }
}