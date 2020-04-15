using System;
using System.Collections.Generic;

namespace AWSXRay.SQS.Publisher.Extensions.Models.Internal
{
    internal class ServiceHandler
    {
        public IDictionary<string, OperationHandler> Operations { get; set; } =
            new Dictionary<string, OperationHandler>(StringComparer.OrdinalIgnoreCase);
    }
}
