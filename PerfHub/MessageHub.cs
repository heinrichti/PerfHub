using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace PerfHub
{
    public static class MessageHub
    {
        public static IMessageHub Create()
          => (IMessageHub)typeof(MessageHub)
            .GetMethod("CreateInner", BindingFlags.NonPublic | BindingFlags.Static)
            .MakeGenericMethod(DynamicTypeBuilder.CreateType())
            .Invoke(null, null);

        private static MessageHubInner<TU> CreateInner<TU>()
          => new MessageHubInner<TU>();

        private class MessageHubInner<TU> : IMessageHub
        {
            private static class Subscriptions<T>
            {
                private static readonly Dictionary<Guid, Action<T>> GlobalSubscriptions = new Dictionary<Guid, Action<T>>();
                private static int _subscriptionsChangeCounter;

                private static readonly ThreadLocal<List<(Action<T> action, Guid guid)>> _localSubscriptions =
                    new ThreadLocal<List<(Action<T> action, Guid guid)>>(() => new List<(Action<T> action, Guid guid)>(), true);

                private static readonly ThreadLocal<int> _localSubscriptionRevision =
                    new ThreadLocal<int>(() => 0, true);

                public static Guid Subscribe(Action<T> action)
                {
                    var guid = Guid.NewGuid();

                    lock (GlobalSubscriptions)
                    {
                        GlobalSubscriptions.Add(guid, action);
                        ++_subscriptionsChangeCounter;
                    }

                    return guid;
                }

                private static List<(Action<T> action, Guid guid)> GetTheLatestSubscriptions()
                {
                    var changeCounterLatestCopy = Interlocked.CompareExchange(
                        ref _subscriptionsChangeCounter, 0, 0);

                    if (_localSubscriptionRevision.Value == changeCounterLatestCopy)
                    {
                        return _localSubscriptions.Value;
                    }

                    List<(Action<T> action, Guid guid)> latestSubscriptions;
                    lock (GlobalSubscriptions)
                    {
                        latestSubscriptions = new List<(Action<T> action, Guid guid)>(GlobalSubscriptions.Select(x => (x.Value, x.Key)));
                    }

                    _localSubscriptionRevision.Value = changeCounterLatestCopy;
                    _localSubscriptions.Value = latestSubscriptions;
                    return _localSubscriptions.Value;
                }


                public static void Unsubscribe(Guid token)
                {
                    lock (GlobalSubscriptions)
                    {
                        GlobalSubscriptions.Remove(token);
                        ++_subscriptionsChangeCounter;
                    }
                }

                internal static void Publish(T message)
                {
                    var subscriptions = GetTheLatestSubscriptions();

                    List<SubscriberInvocationException> errors = null;
                    for (int i = 0; i < subscriptions.Count; i++)
                    {
                        try
                        {
                            subscriptions[i].action(message);
                        }
                        catch (Exception e)
                        {
                            if (errors == null)
                                 errors = new List<SubscriberInvocationException>();
                            errors.Add(new SubscriberInvocationException(subscriptions[i].guid, e));
                        }
                    }

                    if (errors != null)
                        throw new PublishException(errors);
                }
            }

            private static Action<Type, object> _globalHandler;

            public void RegisterGlobalHandler(Action<Type, object> globalHandler)
            {
                _globalHandler = globalHandler;
            }

            public Guid Subscribe<T>(Action<T> action)
            {
                return Subscriptions<T>.Subscribe(action);
            }

            public void Unsubscribe<T>(Guid token)
            {
                Subscriptions<T>.Unsubscribe(token);
            }

            public void Publish<T>(T message)
            {
                _globalHandler?.Invoke(typeof(T), message);
                Subscriptions<T>.Publish(message);
            }
        }
    }
}