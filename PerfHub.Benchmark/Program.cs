using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PerfHub.Performance
{
    [MemoryDiagnoser]
    public class Program
    {
        private Easy.MessageHub.MessageHub _hubSinglePublisherSingleSubscriber = new Easy.MessageHub.MessageHub();
        private IMessageHub _hubSinglePublisherSingleSubscriberSimple = MessageHub.Create();

        private Easy.MessageHub.MessageHub _hubSinglePublisherMultipleSubscriber = new Easy.MessageHub.MessageHub();
        private IMessageHub _hubSinglePublisherMultipleSubscriberSimple = MessageHub.Create();

        private Easy.MessageHub.MessageHub _hubMultiplePublisherSingleSubscriber = new Easy.MessageHub.MessageHub();
        private IMessageHub _hubMultiplePublisherSingleSubscriberSimple = MessageHub.Create();

        private Easy.MessageHub.MessageHub _hubMultiplePublisherSingleSubscriberAndGlobalAuditHandler = new Easy.MessageHub.MessageHub();
        private IMessageHub _hubMultiplePublisherSingleSubscriberAndGlobalAuditHandlerSimple = MessageHub.Create();

        static void Main(string[] args)
        {
            //var program = new Program();
            //program.Setup();

            BenchmarkRunner.Run<Program>();
        }


        [GlobalSetup]
        public void Setup()
        {
            void Increment(string message)
            {
            }

            _hubSinglePublisherSingleSubscriber.Subscribe<string>(Increment);
            _hubSinglePublisherSingleSubscriberSimple.Subscribe<string>(Increment);


            void IncrementMultiple(int msg)
            {
            }

            _hubSinglePublisherMultipleSubscriber.Subscribe<int>(IncrementMultiple);
            _hubSinglePublisherMultipleSubscriber.Subscribe<int>(IncrementMultiple);
            _hubSinglePublisherMultipleSubscriber.Subscribe<int>(IncrementMultiple);
            _hubSinglePublisherMultipleSubscriberSimple.Subscribe<int>(IncrementMultiple);
            _hubSinglePublisherMultipleSubscriberSimple.Subscribe<int>(IncrementMultiple);
            _hubSinglePublisherMultipleSubscriberSimple.Subscribe<int>(IncrementMultiple);

            _hubMultiplePublisherSingleSubscriber.Subscribe<string>(Increment);
            _hubMultiplePublisherSingleSubscriberSimple.Subscribe<string>(Increment);

            _hubMultiplePublisherSingleSubscriberAndGlobalAuditHandler.RegisterGlobalHandler((type, msg) => { });
            _hubMultiplePublisherSingleSubscriberAndGlobalAuditHandlerSimple.RegisterGlobalHandler((type, msg) => { });

            _hubMultiplePublisherSingleSubscriberAndGlobalAuditHandler.Subscribe<string>(x => { });
            _hubMultiplePublisherSingleSubscriberAndGlobalAuditHandlerSimple.Subscribe<string>(x => { });
        }

        [Benchmark]
        public void HubMultiplePublisherSingleSubscriberAndGlobalAuditHandler()
        {
            Action action = () =>
            {
                _hubMultiplePublisherSingleSubscriberAndGlobalAuditHandler.Publish("Hello there!");
            };

            Parallel.Invoke(action, action, action, action, action);
        }

        [Benchmark]
        public void HubMultiplePublisherSingleSubscriberAndGlobalAuditHandlerSimple()
        {
            Action action = () =>
            {
                _hubMultiplePublisherSingleSubscriberAndGlobalAuditHandlerSimple.Publish("Hello there!");
            };

            Parallel.Invoke(action, action, action, action, action);
        }

        [Benchmark]
        public async Task HubMultiplePublisherSingleSubscriber()
        {
            var count = 0;
            var tasks = Enumerable.Range(0, 5).Select(n => Task.Run(() =>
            {
                while (Interlocked.Increment(ref count) < 100)
                {
                    _hubMultiplePublisherSingleSubscriber.Publish("Hello there!");
                }
            }));

            await Task.WhenAll(tasks);
        }

        [Benchmark]
        public async Task HubMultiplePublisherSingleSubscriberSimple()
        {
            var count = 0;
            var tasks = Enumerable.Range(0, 5).Select(n => Task.Run(() =>
            {
                while (Interlocked.Increment(ref count) < 100)
                {
                    _hubMultiplePublisherSingleSubscriberSimple.Publish("Hello there!");
                }
            }));

            await Task.WhenAll(tasks);
        }

        [Benchmark]
        public void HubSinglePublisherMultipleSubscriber()
        {
            _hubSinglePublisherMultipleSubscriber.Publish(35);
        }

        [Benchmark]
        public void HubSinglePublisherMultipleSubscriberSimple()
        {
            _hubSinglePublisherMultipleSubscriberSimple.Publish(35);
        }

        [Benchmark(Baseline = true)]
        public void HubSinglePublisherSingleSubscriber()
        {
            _hubSinglePublisherSingleSubscriber.Publish("Hello there!");
        }

        [Benchmark]
        public void HubSinglePublisherSingleSubscriberSimple()
        {
            _hubSinglePublisherSingleSubscriberSimple.Publish("Hello there!");
        }
    }
}
