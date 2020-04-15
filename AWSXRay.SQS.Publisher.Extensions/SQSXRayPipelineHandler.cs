using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Amazon.Runtime;
using Amazon.Runtime.Internal;
using Amazon.Runtime.Internal.Transform;
using Amazon.Runtime.Internal.Util;
using Amazon.XRay.Recorder.Core;
using Amazon.XRay.Recorder.Core.Exceptions;
using Amazon.XRay.Recorder.Core.Internal.Entities;
using Amazon.XRay.Recorder.Core.Internal.Utils;
using AWSXRay.SQS.Publisher.Extensions.Extensions;
using AWSXRay.SQS.Publisher.Extensions.Models.Internal;

namespace AWSXRay.SQS.Publisher.Extensions
{
    // ReSharper disable once InconsistentNaming
    public class SQSXRayPipelineHandler : PipelineHandler
    {
        private readonly AWSXRayRecorder _recorder;
        private const string S3RequestIdHeaderKey = "x-amz-request-id";
        private const string S3ExtendedRequestIdHeaderKey = "x-amz-id-2";
        private const string ExtendedRequestIdSegmentKey = "id_2";
        private readonly ServiceHandler _serviceHandler;

        private static ServiceHandler InitializeServiceHandler()
        {
            var sendMessageHandler = new OperationHandler
            {
                RequestParameters = new List<string>
                {
                    "DelaySeconds",
                    "QueueUrl"
                },
                RequestDescriptors = new Dictionary<string, ParameterDescriptor>
                {
                    {
                        "MessageAttributes", new ParameterDescriptor
                        {
                            Map = true,
                            GetKeys = true,
                            RenameTo = "message_attribute_names"
                        }
                    }
                },
                ResponseParameters = new List<string>
                {
                    "MessageId"
                }
            };

            var sendMessageBatchHandler = new OperationHandler
            {
                RequestParameters = new List<string>
                {
                    "QueueUrl"
                },
                RequestDescriptors = new Dictionary<string, ParameterDescriptor>
                {
                    {
                        "Entries",
                        new ParameterDescriptor
                        {
                            List = true,
                            GetCount = true,
                            RenameTo = "message_count"
                        }
                    }
                },
                ResponseDescriptors = new Dictionary<string, ParameterDescriptor>
                {
                    {
                        "Failed",
                        new ParameterDescriptor
                        {
                            List = true,
                            GetCount = true,
                            RenameTo = "failed_count"
                        }
                    },
                    {
                        "Successful",
                        new ParameterDescriptor
                        {
                            List = true,
                            GetCount = true,
                            RenameTo = "successful_count"
                        }
                    }
                }
            };

            return new ServiceHandler
            {
                Operations = new Dictionary<string, OperationHandler>
                {
                    { "SendMessage", sendMessageHandler },
                    { "SendMessageBatch", sendMessageBatchHandler }
                }
            };
        }

        public SQSXRayPipelineHandler()
        {
            _recorder = AWSXRayRecorder.Instance;
            _serviceHandler = InitializeServiceHandler();
        }

        private static bool TryReadPropertyValue(object obj, string propertyName, out object value)
        {
            value = default;

            try
            {
                if (obj == null || propertyName == null)
                {
                    return false;
                }

                var property = obj.GetType().GetProperty(propertyName);

                if (property == null)
                {
                    return false;
                }

                value = property.GetValue(obj);
                
                return true;
            }
            catch (ArgumentNullException)
            {
                return false;
            }
            catch (AmbiguousMatchException)
            {
                return false;
            }
        }

        private static string RemoveAmazonPrefixFromServiceName(string serviceName)
        {
            return RemovePrefix(RemovePrefix(serviceName, "Amazon"), ".");
        }

        private static string RemovePrefix(string originalString, string prefix)
        {
            if (prefix == null)
            {
                throw new ArgumentNullException(nameof(prefix));
            }

            if (originalString == null)
            {
                throw new ArgumentNullException(nameof(originalString));
            }

            return 
                originalString.StartsWith(prefix) 
                    ? originalString.Substring(prefix.Length) 
                    : originalString;
        }

        private static string RemoveSuffix(string originalString, string suffix)
        {
            if (suffix == null)
            {
                throw new ArgumentNullException(nameof(suffix));
            }

            if (originalString == null)
            {
                throw new ArgumentNullException(nameof(originalString));
            }

            return 
                originalString.EndsWith(suffix) 
                    ? originalString.Substring(0, originalString.Length - suffix.Length) 
                    : originalString;
        }

        private static void AddMapKeyProperty(IDictionary<string, object> aws, object obj, string propertyName, string renameTo = null)
        {
            if (!TryReadPropertyValue(obj, propertyName, out var propertyValue))
            {
                return;
            }

            if (!(propertyValue is IDictionary dictionaryValue))
            {
                return;
            }

            var newPropertyName = string.IsNullOrEmpty(renameTo) 
                                    ? propertyName 
                                    : renameTo;

            aws[newPropertyName.FromCamelCaseToSnakeCase()] = dictionaryValue.Keys;
        }

        private static void AddListLengthProperty(IDictionary<string, object> aws, object obj, string propertyName, string renameTo = null)
        {
            if (!TryReadPropertyValue(obj, propertyName, out var propertyValue))
            {
                return;
            }

            if (!(propertyValue is IList listValue))
            {
                return;
            }

            var newPropertyName = string.IsNullOrEmpty(renameTo) 
                                    ? propertyName 
                                    : renameTo;

            aws[newPropertyName.FromCamelCaseToSnakeCase()] = listValue.Count;
        }

        private void ProcessBeginRequest(IExecutionContext executionContext)
        {
            var request = executionContext.RequestContext.Request;
            Entity entity = null;

            try
            {
                entity = _recorder.GetEntity();
            }
            catch (EntityNotAvailableException e)
            {
                _recorder.TraceContext.HandleEntityMissing(_recorder, e, "Cannot get entity while processing AWS SDK request");
            }

            _recorder.BeginSubsegment(RemoveAmazonPrefixFromServiceName(request.ServiceName));
            _recorder.SetNamespace("aws");

            entity = entity == null ? null : _recorder.GetEntity();

            if (TraceHeader.TryParse(entity, out var traceHeader))
            {
                request.Headers[TraceHeader.HeaderKey] = traceHeader.ToString();

                if (request.Parameters.Any(p => p.Value == "AWSTraceHeader"))
                {
                    return;
                }

                var index = request
                                .Parameters
                                .Where(x => x.Key.StartsWith("MessageSystemAttribute"))
                                .Select(x => int.TryParse(x.Value.Split('.')[1], out var idx) ? idx : 0)
                                .Union(new[] {0})
                                .Max() + 1;

                request.Parameters.Add("MessageSystemAttribute" + "." + index + "." + "Name",
                    StringUtils.FromString("AWSTraceHeader"));
                request.Parameters.Add("MessageSystemAttribute" + "." + index + "." + "Value.DataType",
                    StringUtils.FromString("String"));
                request.Parameters.Add("MessageSystemAttribute" + "." + index + "." + "Value" + "." + "StringValue",
                    StringUtils.FromString(traceHeader.ToString()));
            }
        }

        private void ProcessEndRequest(IExecutionContext executionContext)
        {
            Entity subSegment;
            
            try
            {
                subSegment = _recorder.GetEntity();
            }
            catch(EntityNotAvailableException e)
            {
                _recorder.TraceContext.HandleEntityMissing(_recorder,e,"Cannot get entity from the trace context while processing response of AWS SDK request.");
                return;
            }

            var responseContext = executionContext.ResponseContext;
            var requestContext = executionContext.RequestContext;

            if (responseContext == null)
            {
                return;
            }

            var client = executionContext.RequestContext.ClientConfig;
            if (client == null)
            {
                return;
            }

            var serviceName = RemoveAmazonPrefixFromServiceName(requestContext.Request.ServiceName);
            var operation = RemoveSuffix(requestContext.OriginalRequest.GetType().Name, "Request");

            subSegment.Aws["region"] = client.RegionEndpoint?.SystemName;
            subSegment.Aws["operation"] = operation;

            if (responseContext.Response == null)
            {
                if (requestContext.Request.Headers.TryGetValue("x-amzn-RequestId", out var requestId))
                {
                    subSegment.Aws["request_id"] = requestId;
                }
                // s3 doesn't follow request header id convention
                else
                {
                    if (requestContext.Request.Headers.TryGetValue(S3RequestIdHeaderKey, out requestId))
                    {
                        subSegment.Aws["request_id"] = requestId;
                    }

                    if (requestContext.Request.Headers.TryGetValue(S3ExtendedRequestIdHeaderKey, out requestId))
                    {
                        subSegment.Aws[ExtendedRequestIdSegmentKey] = requestId;
                    }
                }
            }
            else
            {
                subSegment.Aws["request_id"] = responseContext.Response.ResponseMetadata.RequestId;
                AddResponseSpecificInformation(serviceName, operation, responseContext.Response, subSegment.Aws);
            }

            if (responseContext.HttpResponse != null)
            {
                AddHttpInformation(responseContext.HttpResponse);
            }

            AddRequestSpecificInformation(serviceName, operation, requestContext.OriginalRequest, subSegment.Aws);

            _recorder.EndSubsegment();
        }

        private void AddHttpInformation(IWebResponseData httpResponse)
        {
            var responseAttributes = new Dictionary<string, object>();
            var statusCode = httpResponse.StatusCode;
            
            if (!statusCode.IsSuccessStatusCode())
            {
                _recorder.MarkError();

                if (statusCode.IsTooManyRequests())
                {
                    _recorder.MarkThrottle();
                }
            }
            else if (statusCode.IsFault())
            {
                _recorder.MarkFault();
            }

            responseAttributes["status"] = statusCode;
            responseAttributes["content_length"] = httpResponse.ContentLength;
            _recorder.AddHttpInformation("response", responseAttributes);
        }

        private void ProcessException(AmazonServiceException ex, Entity subSegment)
        {
            var statusCode = ex.StatusCode;
            var responseAttributes = new Dictionary<string, object>();

            if (!statusCode.IsSuccessStatusCode())
            {
                _recorder.MarkError();
                if (statusCode.IsTooManyRequests())
                {
                    _recorder.MarkThrottle();
                }
            }
            else if (statusCode.IsFault())
            {
                _recorder.MarkFault();
            }

            responseAttributes["status"] = statusCode;
            _recorder.AddHttpInformation("response", responseAttributes);

            subSegment.Aws["request_id"] = ex.RequestId;
        }

        private void AddRequestSpecificInformation(string serviceName, string operation, AmazonWebServiceRequest request, IDictionary<string, object> aws)
        {
            if (serviceName == null)
            {
                throw new ArgumentNullException(nameof(serviceName));
            }

            if (operation == null)
            {
                throw new ArgumentNullException(nameof(operation));
            }

            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (aws == null)
            {
                throw new ArgumentNullException(nameof(aws));
            }

            if(!_serviceHandler.Operations.TryGetValue(operation, out var operationHandler))
            {
                return;
            }

            if (operationHandler.RequestParameters != null)
            {
                foreach (var parameter in operationHandler.RequestParameters)
                {
                    if (TryReadPropertyValue(request, parameter, out var propertyValue))
                    {
                        aws[parameter.FromCamelCaseToSnakeCase()] = propertyValue;
                    }
                }
            }

            if (operationHandler.RequestDescriptors != null)
            {
                foreach (var kv in operationHandler.ResponseDescriptors)
                {
                    var propertyName = kv.Key;
                    var descriptor = kv.Value;

                    if (descriptor.Map && descriptor.GetKeys)
                    {
                        AddMapKeyProperty(aws, request, propertyName, descriptor.RenameTo);
                    }
                    else if (descriptor.List && descriptor.GetCount)
                    {
                        AddListLengthProperty(aws, request, propertyName, descriptor.RenameTo);
                    }
                }
            }
        }

        private void AddResponseSpecificInformation(string serviceName, string operation, AmazonWebServiceResponse response, IDictionary<string, object> aws)
        {
            if (serviceName == null)
            {
                throw new ArgumentNullException(nameof(serviceName));
            }

            if (operation == null)
            {
                throw new ArgumentNullException(nameof(operation));
            }

            if (response == null)
            {
                throw new ArgumentNullException(nameof(response));
            }

            if (aws == null)
            {
                throw new ArgumentNullException(nameof(aws));
            }

            if (!_serviceHandler.Operations.TryGetValue(operation, out var operationHandler))
            {
                return;
            }

            if (operationHandler.ResponseParameters != null)
            {
                foreach (var parameter in operationHandler.ResponseParameters)
                {
                    if (TryReadPropertyValue(response, parameter, out var propertyValue))
                    {
                        aws[parameter.FromCamelCaseToSnakeCase()] = propertyValue;
                    }
                }
            }

            if (operationHandler.ResponseDescriptors != null)
            {
                foreach (var kv in operationHandler.ResponseDescriptors)
                {
                    var propertyName = kv.Key;
                    var descriptor = kv.Value;

                    if (descriptor.Map && descriptor.GetKeys)
                    {
                        AddMapKeyProperty(aws, response, propertyName, descriptor.RenameTo);
                    }
                    else if (descriptor.List && descriptor.GetCount)
                    {
                        AddListLengthProperty(aws, response, propertyName, descriptor.RenameTo);
                    }
                }
            }
        }

        public override void InvokeSync(IExecutionContext executionContext)
        {
            if (IsTracingDisabled() || ExcludeServiceOperation(executionContext))
            {
                base.InvokeSync(executionContext);
            }
            else
            {
                ProcessBeginRequest(executionContext);

                try
                {
                    base.InvokeSync(executionContext);
                }

                catch (Exception e)
                {
                    PopulateException(e);

                    throw;
                }

                finally
                {
                    ProcessEndRequest(executionContext);
                }
            }
        }

        private void PopulateException(Exception e)
        {
            Entity subSegment;

            try
            {
                subSegment = _recorder.GetEntity();
            }
            catch (EntityNotAvailableException ex)
            {
                _recorder
                    .TraceContext
                    .HandleEntityMissing
                    (
                        _recorder, 
                        ex, 
                        "Cannot get entity from trace context while processing exception for AWS SDK request."
                    );

                return;
            }

            subSegment.AddException(e); // record exception 

            if (e is AmazonServiceException amazonServiceException)
            {
                ProcessException(amazonServiceException, subSegment);
            }
        }

        private bool ExcludeServiceOperation(IExecutionContext executionContext)
        {
            var requestContext = executionContext.RequestContext;

            var serviceName = RemoveAmazonPrefixFromServiceName(requestContext.Request.ServiceName);
            var operation = RemoveSuffix(requestContext.OriginalRequest.GetType().Name, "Request");

            return !(serviceName == "SQS" && _serviceHandler.Operations.ContainsKey(operation));
        }

        private static bool IsTracingDisabled()
        {
            return 
                AWSXRayRecorder
                    .Instance
                        .IsTracingDisabled();
        }

        public override async Task<T> InvokeAsync<T>(IExecutionContext executionContext)
        {
            T returnValue;

            if (IsTracingDisabled() || ExcludeServiceOperation(executionContext))
            {
                returnValue = await 
                                base
                                    .InvokeAsync<T>(executionContext)
                                        .ConfigureAwait(false);
            }
            else
            {
                ProcessBeginRequest(executionContext);

                try
                {
                    returnValue = await 
                                    base
                                        .InvokeAsync<T>(executionContext)
                                            .ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    PopulateException(e);

                    throw;
                }
                finally
                {
                    ProcessEndRequest(executionContext);
                }
            }

            return returnValue;
        }
    }
}
