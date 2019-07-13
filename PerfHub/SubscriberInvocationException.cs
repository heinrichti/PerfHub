using System;

namespace PerfHub
{
    public class SubscriberInvocationException : Exception
    {
        public SubscriberInvocationException(Guid guid, Exception innerException)
            : base("Error calling subscriber " + guid.ToString(), innerException)
        {
            Guid = guid;
        }

        public Guid Guid { get; }
    }
}
