using System;
using System.Collections.Generic;
using System.Text;

namespace PerfHub
{
    public class PublishException : Exception
    {
        public PublishException(List<SubscriberInvocationException> exceptions)
        {
            Exceptions = exceptions;
        }

        public List<SubscriberInvocationException> Exceptions { get; }
    }
}
