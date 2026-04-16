using System;

namespace Foundatio.Storage;

public class AliyunFileStorageOptions : SharedOptions
{
    public string? ConnectionString { get; set; }
}

public class AliyunFileStorageOptionsBuilder : SharedOptionsBuilder<AliyunFileStorageOptions, AliyunFileStorageOptionsBuilder>
{
    public AliyunFileStorageOptionsBuilder ConnectionString(string? connectionString)
    {
        Target.ConnectionString = String.IsNullOrEmpty(connectionString) ? null : connectionString;
        return this;
    }
}
