using System;
using Amazon.Runtime.Internal;

namespace AWSXRay.SQS.Publisher.Extensions
{
    // ReSharper disable once InconsistentNaming
    internal class SQSXRayPipelineCustomizer : IRuntimePipelineCustomizer
    {
        public string UniqueName => typeof(SQSXRayPipelineCustomizer).FullName;
   
        public void Customize(Type serviceClientType, RuntimePipeline pipeline)
        {
            pipeline.AddHandlerAfter<EndpointResolver>(new SQSXRayPipelineHandler());
        }
   }
}