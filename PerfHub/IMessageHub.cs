using System;

namespace PerfHub
{
    public interface IMessageHub
    {
        void Publish<T>(T message);
        void RegisterGlobalHandler(Action<Type, object> globalHandler);
        Guid Subscribe<T>(Action<T> action);
        void Unsubscribe<T>(Guid token);
    }
}