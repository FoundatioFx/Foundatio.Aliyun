using System.Net;

namespace Foundatio.Aliyun.Extensions;

internal static class Extensions
{
    internal static bool IsSuccessful(this HttpStatusCode code)
    {
        return (int)code < 400;
    }
}
