using System;
using Aliyun.OSS;

namespace Foundatio {
    internal class AliyunConnectionString {
        public string Endpoint { get; set; }

        public string AccessKeyId { get; set; }

        public string AccessKeySecret { get; set; }


        public OssClient CreateClient() {
            return new OssClient(Endpoint, AccessKeyId, AccessKeySecret);
        }

        public static AliyunConnectionString Parse(string connectionString) {
            if (string.IsNullOrWhiteSpace(connectionString)) {
                throw new ArgumentNullException(nameof(connectionString));
            }
            var options = new AliyunConnectionString();
            // break it down by commas
            foreach (var optionText in connectionString.Split(',', ';')) {
                if (string.IsNullOrWhiteSpace(optionText)) continue;
                var optionString = optionText.Trim();
                int index = optionString.IndexOf('=');
                if (index > 0) {
                    var key = optionString.Substring(0, index).Trim();
                    var value = optionString.Substring(index + 1).Trim();
                    if (string.Equals(key, "AccessKeyId", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(key, "Access Key Id", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(key, "Id", StringComparison.OrdinalIgnoreCase)) {
                        options.AccessKeyId = value;
                    }
                    else if (string.Equals(key, "AccessKeySecret", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(key, "Access Key Secret", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(key, "Secret", StringComparison.OrdinalIgnoreCase)) {
                        options.AccessKeySecret = value;
                    }
                    else if (string.Equals(key, "EndPoint", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(key, "End Point", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(key, "Address", StringComparison.OrdinalIgnoreCase)) {

                    }
                }
            }
            return options;
        }
    }
}
