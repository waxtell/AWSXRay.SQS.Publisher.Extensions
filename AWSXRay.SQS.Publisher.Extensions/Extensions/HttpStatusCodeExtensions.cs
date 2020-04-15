using System.Net;

namespace AWSXRay.SQS.Publisher.Extensions.Extensions
{
    internal static class HttpStatusCodeExtensions
    {
        public static bool IsSuccessStatusCode(this HttpStatusCode statusCode)
        {
            return (int) statusCode >= 200 && (int) statusCode <= 299;
        }

        public static bool IsTooManyRequests(this HttpStatusCode statusCode)
        {
            return (int) statusCode == 429;
        }

        public static bool IsFault(this HttpStatusCode statusCode)
        {
            return (int) statusCode >= 500 && (int) statusCode <= 599;
        }
    }
}
