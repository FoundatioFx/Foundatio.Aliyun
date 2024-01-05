using Foundatio.Storage;
using Xunit;

namespace Foundatio.Aliyun.Tests.Storage
{
    public class StorageConnectionStringBuilderTests : ConnectionStringBuilderTests
    {
        protected override AliyunConnectionStringBuilder CreateConnectionStringBuilder(string connectionString)
        {
            return new AliyunFileStorageConnectionStringBuilder(connectionString);
        }

        protected override AliyunConnectionStringBuilder CreateConnectionStringBuilder()
        {
            return new AliyunFileStorageConnectionStringBuilder();
        }

        [Fact]
        public override void InvalidKeyShouldThrow()
        {
            base.InvalidKeyShouldThrow();
        }

        [Fact]
        public override void CanParseAccessKey()
        {
            base.CanParseAccessKey();
        }

        [Fact]
        public override void CanParseSecretKey()
        {
            base.CanParseSecretKey();
        }

        [Fact]
        public override void CanParseRegion()
        {
            base.CanParseRegion();
        }

        [Fact]
        public override void CanGenerateConnectionString()
        {
            base.CanGenerateConnectionString();
        }

        [Fact]
        public void CanParseBucket()
        {
            foreach (string key in new[] { "Bucket", "bucket" })
            {
                var connectionStringBuilder = CreateConnectionStringBuilder($"AccessKey=TestAccessKey;SecretKey=TestSecretKey;{key}=TestBucket");
                Assert.Equal("TestAccessKey", connectionStringBuilder.AccessKey);
                Assert.Equal("TestSecretKey", connectionStringBuilder.SecretKey);
                Assert.Equal("TestBucket", ((AliyunFileStorageConnectionStringBuilder)connectionStringBuilder).Bucket);
                Assert.Null(connectionStringBuilder.Endpoint);
            }
        }

        [Fact]
        public void CanGenerateConnectionStringWithBucket()
        {
            var connectionStringBuilder = (AliyunFileStorageConnectionStringBuilder)CreateConnectionStringBuilder();
            connectionStringBuilder.AccessKey = "TestAccessKey";
            connectionStringBuilder.SecretKey = "TestSecretKey";
            connectionStringBuilder.Endpoint = "TestEndPoint";
            connectionStringBuilder.Bucket = "TestBucket";

            Assert.Equal("AccessKey=TestAccessKey;SecretKey=TestSecretKey;EndPoint=TestEndPoint;Bucket=TestBucket;", connectionStringBuilder.ToString());
        }
    }
}
