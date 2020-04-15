using System.Collections.Generic;

namespace AWSXRay.SQS.Publisher.Extensions.Models.Internal
{
    internal class OperationHandler
    {
        public IEnumerable<string> RequestParameters { get; set; }
        public IEnumerable<string> ResponseParameters { get; set; }
        public IDictionary<string, ParameterDescriptor> RequestDescriptors { get; set; }
        public IDictionary<string, ParameterDescriptor> ResponseDescriptors { get; set; }
    }
}
