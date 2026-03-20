using System;
using Amazon.Runtime.Internal;
using Amazon.SQS;
#if NET8_0_OR_GREATER
using Amazon.SQS.Internal;
#endif

namespace AWSXRay.SQS.Publisher.Extensions
{
    // ReSharper disable once InconsistentNaming
    internal class SQSXRayPipelineCustomizer : IRuntimePipelineCustomizer
    {
        public string UniqueName => typeof(SQSXRayPipelineCustomizer).FullName;

        public void Customize(Type serviceClientType, RuntimePipeline pipeline)
        {
            if (!typeof(AmazonSQSClient).IsAssignableFrom(serviceClientType))
                return;

#if NET8_0_OR_GREATER
            // AWSSDK.SQS v4 removes EndpointResolver and replaces it with AmazonSQSEndpointResolver
            pipeline.AddHandlerAfter<AmazonSQSEndpointResolver>(new SQSXRayPipelineHandler());
#else
            pipeline.AddHandlerAfter<EndpointResolver>(new SQSXRayPipelineHandler());
#endif
        }
   }
}