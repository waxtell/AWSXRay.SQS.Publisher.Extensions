using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Amazon.Runtime;
using Amazon.Runtime.Internal;

namespace AWSXRay.SQS.Publisher.Extensions
{
    // ReSharper disable once InconsistentNaming
    internal class SQSXRayPipelineCustomizer : IRuntimePipelineCustomizer
    {
        public string UniqueName => typeof(SQSXRayPipelineCustomizer).FullName;
        private readonly List<Type> _types = new List<Type>();
        private readonly ReaderWriterLockSlim _rwLock = new ReaderWriterLockSlim();
   
        public void Customize(Type serviceClientType, RuntimePipeline pipeline)
        {
            if (serviceClientType.BaseType != typeof(AmazonServiceClient))
            {
                return;
            }

            var addCustomization = ProcessType(serviceClientType, false);
            if (addCustomization)
            {
                pipeline.AddHandlerAfter<EndpointResolver>(new SQSXRayPipelineHandler());
            }
        }

        private bool ProcessType(Type serviceClientType, bool addCustomization)
        {
            _rwLock.EnterReadLock();

            try
            {
                if (_types.Any(registeredType => registeredType.IsAssignableFrom(serviceClientType)))
                {
                    addCustomization = true;
                }
            }
            finally
            {
                _rwLock.ExitReadLock();
            }

            return addCustomization;
        }

        public void AddType(Type type)
        {
            _rwLock.EnterWriteLock();

            try
            {
                _types.Add(type);
            }
            finally
            {
                _rwLock.ExitWriteLock();
            }
        }
    }
}