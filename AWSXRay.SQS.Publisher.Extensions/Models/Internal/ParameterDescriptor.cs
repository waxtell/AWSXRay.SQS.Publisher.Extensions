namespace AWSXRay.SQS.Publisher.Extensions.Models.Internal
{
    internal class ParameterDescriptor
    {
        public string RenameTo { get; set; }
        public bool Map { get; set; }
        public bool GetKeys { get; set; }
        public bool List { get; set; }
        public bool GetCount { get; set; }
    }
}
