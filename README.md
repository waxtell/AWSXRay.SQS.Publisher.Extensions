# AWSXRay.SQS.Publisher.Extensions
AWS XRay pipeline handler that will proliferate the XRay TraceHeader across SendMessage and SendMessageBatch calls.

![Build](https://github.com/waxtell/AWSXRay.SQS.Publisher.Extensions/workflows/Build/badge.svg)

Simply register the handler before any instance of the IAmazonSQS client has been instantiated.

```csharp
AWSSDKSQSHandler.RegisterXRayForSQS();
```

The handler will add the AWSTraceHeader message system attribute to all SendMessage and SendMessageBatch requests.

Please see [AWSXRay.SQS.Consumer.Extensions](https://github.com/waxtell/AWSXRay.SQS.Consumer.Extensions) for the retrieval of the trace header as well as methods to continue the trace from the SQS message.