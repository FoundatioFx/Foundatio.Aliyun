using System;
using Xunit;

namespace Foundatio.Aliyun.Tests;

public abstract class ConnectionStringBuilderTests
{
    protected abstract AliyunConnectionStringBuilder CreateConnectionStringBuilder(string connectionString);

    protected abstract AliyunConnectionStringBuilder CreateConnectionStringBuilder();

    public virtual void InvalidKeyShouldThrow()
    {
        var exception = Assert.Throws<ArgumentException>("connectionString", () => CreateConnectionStringBuilder("wrongaccess=TestAccessKey;SecretKey=TestSecretKey"));
        Assert.Equal("The option 'wrongaccess' cannot be recognized in connection string. (Parameter 'connectionString')", exception.Message);
    }

    public virtual void CanParseAccessKey()
    {
        foreach (string key in new[] { "AccessKey", "AccessKeyId", "Access Key", "Access Key ID", "Id", "accessKey", "access key", "access key id", "id" })
        {
            var connectionStringBuilder = CreateConnectionStringBuilder($"{key}=TestAccessKey;SecretKey=TestSecretKey;");
            Assert.Equal("TestAccessKey", connectionStringBuilder.AccessKey);
            Assert.Equal("TestSecretKey", connectionStringBuilder.SecretKey);
            Assert.Null(connectionStringBuilder.Endpoint);
        }
    }

    public virtual void CanParseSecretKey()
    {
        foreach (string key in new[] { "SecretKey", "Secret Key", "Secret", "secretKey", "secret key", "secret" })
        {
            var connectionStringBuilder = CreateConnectionStringBuilder($"AccessKey=TestAccessKey;{key}=TestSecretKey;");
            Assert.Equal("TestAccessKey", connectionStringBuilder.AccessKey);
            Assert.Equal("TestSecretKey", connectionStringBuilder.SecretKey);
            Assert.Null(connectionStringBuilder.Endpoint);
        }
    }

    public virtual void CanParseRegion()
    {
        foreach (string key in new[] { "EndPoint", "End Point", "endPoint", "end point" })
        {
            var connectionStringBuilder = CreateConnectionStringBuilder($"AccessKey=TestAccessKey;SecretKey=TestSecretKey;{key}=TestEndPoint;");
            Assert.Equal("TestAccessKey", connectionStringBuilder.AccessKey);
            Assert.Equal("TestSecretKey", connectionStringBuilder.SecretKey);
            Assert.Equal("TestEndPoint", connectionStringBuilder.Endpoint);
        }
    }

    public virtual void CanGenerateConnectionString()
    {
        var connectionStringBuilder = CreateConnectionStringBuilder();
        connectionStringBuilder.AccessKey = "TestAccessKey";
        connectionStringBuilder.SecretKey = "TestSecretKey";
        connectionStringBuilder.Endpoint = "TestEndPoint";

        Assert.Equal("AccessKey=TestAccessKey;SecretKey=TestSecretKey;EndPoint=TestEndPoint;", connectionStringBuilder.ToString());
    }
}
