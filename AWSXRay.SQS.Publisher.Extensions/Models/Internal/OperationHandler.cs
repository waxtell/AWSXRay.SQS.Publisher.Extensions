using System.Collections.Generic;

namespace AWSXRay.SQS.Publisher.Extensions.Models.Internal
{
    internal class OperationHandler
    {
        public IEnumerable<string> RequestParameters { get; set; } = new List<string>();
        public IEnumerable<string> ResponseParameters { get; set; } = new List<string>();
        public IDictionary<string, ParameterDescriptor> RequestDescriptors { get; set; } = new Dictionary<string, ParameterDescriptor>();
        public IDictionary<string, ParameterDescriptor> ResponseDescriptors { get; set; } = new Dictionary<string, ParameterDescriptor>();
    }
}
