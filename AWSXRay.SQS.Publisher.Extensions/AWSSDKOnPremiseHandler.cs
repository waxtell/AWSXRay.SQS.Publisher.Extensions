using Amazon.Runtime.Internal;
using Amazon.SQS;

namespace AWSXRay.SQS.Publisher.Extensions
{
    // ReSharper disable once UnusedMember.Global
    // ReSharper disable once InconsistentNaming
    // ReSharper disable once IdentifierTypo
    public static class AWSSDKSQSHandler
    {
        private static SQSXRayPipelineCustomizer _customizer;

        // ReSharper disable once UnusedMember.Global
        // ReSharper disable once InconsistentNaming
        public static void RegisterXRayForSQS()
        {
            _customizer = GetCustomizer();
            _customizer.AddType(typeof(IAmazonSQS));
        }

        private static SQSXRayPipelineCustomizer GetCustomizer()
        {
            if (_customizer == null)
            {
                _customizer = new SQSXRayPipelineCustomizer();
                
                RuntimePipelineCustomizerRegistry
                    .Instance
                    .Register(_customizer);
            }

            return _customizer;
        }
    }
}
