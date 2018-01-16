using System;

namespace Foundatio.Storage {
    public class AliyunFileStorageOptions : FileStorageOptionsBase {
        public string ConnectionString { get; set; }
        public string Bucket { get; set; } = "storage";
    }
}