using Amazon.Runtime.Internal;

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
            _customizer = new SQSXRayPipelineCustomizer();

            RuntimePipelineCustomizerRegistry
                .Instance
                .Register(_customizer);
        }
    }
}
