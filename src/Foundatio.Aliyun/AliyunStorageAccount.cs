using System;
using Aliyun.OSS;

namespace Foundatio {
    internal class AliyunStorageAccount {
        public string Endpoint { get; set; }

        public string AccessKeyId { get; set; }

        public string AccessKeySecret { get; set; }

        public OssClient CreateClient() {
            return new OssClient(Endpoint, AccessKeyId, AccessKeySecret);
        }

        public static AliyunStorageAccount Parse(string connectionString) {
            if (String.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentNullException(nameof(connectionString));

            var options = new AliyunStorageAccount();

            foreach (string optionText in connectionString.Split(',', ';')) {
                if (String.IsNullOrWhiteSpace(optionText))
                    continue;

                string optionString = optionText.Trim();
                int index = optionString.IndexOf('=');
                if (index <= 0)
                    continue;

                string key = optionString.Substring(0, index).Trim();
                string value = optionString.Substring(index + 1).Trim();

                if (String.Equals(key, "AccessKeyId", StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(key, "Access Key Id", StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(key, "Id", StringComparison.OrdinalIgnoreCase)) {
                    options.AccessKeyId = value;
                } else if (String.Equals(key, "AccessKeySecret", StringComparison.OrdinalIgnoreCase) ||
                           String.Equals(key, "Access Key Secret", StringComparison.OrdinalIgnoreCase) ||
                           String.Equals(key, "Secret", StringComparison.OrdinalIgnoreCase)) {
                    options.AccessKeySecret = value;
                } else if (String.Equals(key, "EndPoint", StringComparison.OrdinalIgnoreCase) ||
                           String.Equals(key, "End Point", StringComparison.OrdinalIgnoreCase) ||
                           String.Equals(key, "Address", StringComparison.OrdinalIgnoreCase)) {
                    options.Endpoint = value;
                }
            }

            return options;
        }
    }
}
