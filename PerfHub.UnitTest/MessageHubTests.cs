using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Linq;

namespace PerfHub.UnitTest
{
    [TestClass]
    public class MessageHubTests
    {
        [TestMethod]
        public void When_publishing_with_no_subscribers()
        {
            var hub = MessageHub.Create();
            hub.Publish(TimeSpan.FromTicks(1234));

            string result = null;
            hub.RegisterGlobalHandler((type, msg) =>
            {
                Assert.AreEqual(typeof(string), type);
                Assert.AreEqual(typeof(string), msg.GetType());
                result = msg as string;
            });

            hub.Publish("654321");

            Assert.AreEqual("654321", result);
        }

        [TestMethod]
        public void When_unsubscribing_invalid_token()
        {
            var hub = MessageHub.Create();
            hub.Unsubscribe<object>(Guid.NewGuid());
        }

        [TestMethod]
        public void When_subscribing_handlers()
        {
            var hub = MessageHub.Create();

            var queue = new ConcurrentQueue<string>();
            Action<string> subscriber = msg => queue.Enqueue(msg);

            hub.Subscribe(subscriber);

            hub.Publish("A");

            Assert.AreEqual(1, queue.Count);

            string receivedMsg;
            Assert.IsTrue(queue.TryDequeue(out receivedMsg));
            Assert.AreEqual("A", receivedMsg);
        }

        [TestMethod]
        public void When_subscribing_handlers_with_one_throwing_exception()
        {
            var hub = MessageHub.Create();

            var queue = new List<string>();
            var totalMsgs = new List<string>();
            var errors = new List<KeyValuePair<Guid, Exception>>();

            hub.RegisterGlobalHandler((type, msg) =>
            {
                Assert.AreEqual(typeof(string), type);
                Assert.AreEqual(typeof(string), msg.GetType());
                totalMsgs.Add((string)msg);
            });

            Action<string> subscriberOne = msg => queue.Add("Sub1-" + msg);
            Action<string> subscriberTwo = msg => { throw new InvalidOperationException("Ooops-" + msg); };
            Action<string> subscriberThree = msg => queue.Add("Sub3-" + msg);

            hub.Subscribe(subscriberOne);
            var subTwoToken = hub.Subscribe(subscriberTwo);
            hub.Subscribe(subscriberThree);

            try
            {
                hub.Publish("A");
            }
            catch (PublishException e)
            {
                errors.AddRange(e.Exceptions.Select(x => new KeyValuePair<Guid, Exception>(x.Guid, x.InnerException)));
            }

            Action<string> subscriberFour = msg => { throw new InvalidCastException("Aaargh-" + msg); };
            var subFourToken = hub.Subscribe(subscriberFour);

            try
            {
                hub.Publish("B");
            }
            catch (PublishException e)
            {
                errors.AddRange(e.Exceptions.Select(x => new KeyValuePair<Guid, Exception>(x.Guid, x.InnerException)));
            }

            Assert.AreEqual(4, queue.Count);
            Assert.AreEqual("Sub1-A", queue[0]);
            Assert.AreEqual("Sub3-A", queue[1]);
            Assert.AreEqual("Sub1-B", queue[2]);
            Assert.AreEqual("Sub3-B", queue[3]);

            Assert.AreEqual(2, totalMsgs.Count);
            Assert.IsTrue(totalMsgs.Contains("A"));
            Assert.IsTrue(totalMsgs.Contains("B"));

            Assert.AreEqual(3, errors.Count);
            Assert.IsTrue(errors.Any(err => err.Value.GetType() == typeof(InvalidOperationException)
                && err.Value.Message == "Ooops-A"
                && err.Key == subTwoToken));

            Assert.IsTrue(errors.Any(err => err.Value.GetType() == typeof(InvalidOperationException)
                && err.Value.Message == "Ooops-B"
                && err.Key == subTwoToken));

            Assert.IsTrue(errors.Any(err => err.Value.GetType() == typeof(InvalidCastException)
                && err.Value.Message == "Aaargh-B"
                && err.Key == subFourToken));

            hub.Unsubscribe<string>(subTwoToken);
        }

        [TestMethod]
        public void When_subscribing_same_handler_multiple_times()
        {
            var hub = MessageHub.Create();

            var totalMsgCount = 0;

            hub.RegisterGlobalHandler((type, msg) =>
            {
                Assert.AreEqual(typeof(string), type);
                Assert.AreEqual(typeof(string), msg.GetType());
                Interlocked.Increment(ref totalMsgCount);
            });

            var queue = new ConcurrentQueue<string>();
            Action<string> subscriber = msg => queue.Enqueue(msg);

            var tokenOne = hub.Subscribe(subscriber);
            var tokenTwo = hub.Subscribe(subscriber);

            hub.Publish("A");

            Assert.AreEqual(2, queue.Count);
            Assert.AreEqual(1, totalMsgCount);
        }

        [TestMethod]
        public void When_testing_global_on_message_event()
        {
            var hub = MessageHub.Create();
            //hub.ClearSubscriptions();

            var msgOne = 0;

            hub.RegisterGlobalHandler((type, msg) =>
            {
                Assert.AreEqual(typeof(string), type);
                Assert.AreEqual(typeof(string), msg.GetType());
                msgOne++;
            });

            hub.Publish("A");

            Assert.AreEqual(1, msgOne);

            hub.Publish("B");

            Assert.AreEqual(2, msgOne);

            var msgTwo = 0;

            hub.RegisterGlobalHandler((type, msg) =>
            {
                Assert.AreEqual(typeof(string), type);
                Assert.AreEqual(typeof(string), msg.GetType());
                msgTwo++;
            });

            hub.RegisterGlobalHandler((type, msg) =>
            {
                Assert.AreEqual(typeof(string), type);
                Assert.AreEqual(typeof(string), msg.GetType());
                msgTwo++;
            });

            hub.Publish("C");

            Assert.AreEqual(1, msgTwo);

            hub.RegisterGlobalHandler((type, msg) =>
            {
                Assert.AreEqual(typeof(string), type);
                Assert.AreEqual(typeof(string), msg.GetType());
                // do nothing with the message
            });

            hub.Publish("D");

            Assert.AreEqual(2, msgOne);
            Assert.AreEqual(1, msgTwo);
        }

        [TestMethod]
        public void When_testing_single_subscriber_with_publisher_on_current_thread()
        {
            var hub = MessageHub.Create();

            var queue = new List<string>();

            Action<string> subscriber = msg => queue.Add(msg);
            hub.Subscribe(subscriber);

            hub.Publish("MessageA");

            Assert.AreEqual(1, queue.Count);
            Assert.AreEqual("MessageA", queue[0]);

            hub.Publish("MessageB");

            Assert.AreEqual(2, queue.Count);
            Assert.AreEqual("MessageB", queue[1]);
        }

        [TestMethod]
        public void When_testing_multiple_subscribers_with_publisher_on_current_thread()
        {
            var hub = MessageHub.Create();

            var queueOne = new List<string>();
            var queueTwo = new List<string>();

            Action<string> subscriberOne = msg => queueOne.Add("Sub1-" + msg);
            Action<string> subscriberTwo = msg => queueTwo.Add("Sub2-" + msg);

            hub.Subscribe(subscriberOne);
            hub.Subscribe(subscriberTwo);

            hub.Publish("MessageA");

            Assert.AreEqual(1, queueOne.Count);
            Assert.AreEqual(1, queueTwo.Count);

            Assert.AreEqual("Sub1-MessageA", queueOne[0]);
            Assert.AreEqual("Sub2-MessageA", queueTwo[0]);

            hub.Publish("MessageB");

            Assert.AreEqual(2, queueOne.Count);
            Assert.AreEqual(2, queueTwo.Count);

            Assert.AreEqual("Sub1-MessageB", queueOne[1]);
            Assert.AreEqual("Sub2-MessageB", queueTwo[1]);
        }

        [TestMethod]
        public void When_testing_multiple_subscribers_with_filters_and_publisher_on_current_thread()
        {
            var hub = MessageHub.Create();

            var queueOne = new List<string>();
            var queueTwo = new List<string>();

            var predicateOne = new Predicate<string>(x => x.Length > 3);
            var predicateTwo = new Predicate<string>(x => x.Length < 3);

            Action<string> subscriberOne = msg =>
            {
                if (predicateOne(msg))
                {
                    queueOne.Add("Sub1-" + msg);
                }
            };

            Action<string> subscriberTwo = msg =>
            {
                if (predicateTwo(msg))
                {
                    queueTwo.Add("Sub2-" + msg);
                }
            };

            hub.Subscribe(subscriberOne);
            hub.Subscribe(subscriberTwo);

            hub.Publish("MessageA");

            Assert.AreEqual(1, queueOne.Count);
            Assert.AreEqual(0, queueTwo.Count);
            Assert.AreEqual("Sub1-MessageA", queueOne[0]);

            hub.Publish("MA");

            Assert.AreEqual(1, queueTwo.Count);
            Assert.AreEqual(1, queueOne.Count);
            Assert.AreEqual("Sub2-MA", queueTwo[0]);

            hub.Publish("MMM");

            Assert.AreEqual(1, queueTwo.Count);
            Assert.AreEqual(1, queueOne.Count);
            
            hub.Publish("MessageB");

            Assert.AreEqual(2, queueOne.Count);
            Assert.AreEqual(1, queueTwo.Count);

            Assert.AreEqual("Sub1-MessageB", queueOne[1]);

            hub.Publish("MB");

            Assert.AreEqual(2, queueOne.Count);
            Assert.AreEqual(2, queueTwo.Count);
            Assert.AreEqual("Sub2-MB", queueTwo[1]);
        }

        [TestMethod]
        public void When_testing_multiple_subscribers_with_one_subscriber_unsubscribing_then_resubscribing()
        {
            var totalMessages = 0;
            var hub = MessageHub.Create();

            hub.RegisterGlobalHandler((type, msg) =>
            {
                Assert.AreEqual(typeof(string), type);
                Assert.AreEqual(typeof(string), msg.GetType());
                Interlocked.Increment(ref totalMessages);
            });

            var queue = new List<string>();

            Action<string> subscriberOne = msg => queue.Add("Sub1-" + msg);
            Action<string> subscriberTwo = msg => queue.Add("Sub2-" + msg);

            var tokenOne = hub.Subscribe(subscriberOne);
            hub.Subscribe(subscriberTwo);

            hub.Publish("A");

            Assert.AreEqual(2, queue.Count);
            Assert.AreEqual("Sub1-A", queue[0]);
            Assert.AreEqual("Sub2-A", queue[1]);

            hub.Unsubscribe<string>(tokenOne);

            hub.Publish("B");

            Assert.AreEqual(3, queue.Count);
            Assert.AreEqual("Sub2-B", queue[2]);

            hub.Subscribe(subscriberOne);

            hub.Publish("C");

            Assert.AreEqual(5, queue.Count);
            Assert.AreEqual("Sub1-C", queue[3]);
            Assert.AreEqual("Sub2-C", queue[4]);

            Assert.AreEqual(3, totalMessages);
        }

        [TestMethod]
        public void When_using_multiple_hubs()
        {
            var hub1 = MessageHub.Create();
            var hub2 = MessageHub.Create();

            var totalMessages = new List<string>();

            hub1.RegisterGlobalHandler((type, msg) => totalMessages.Add((string)msg));
            hub2.RegisterGlobalHandler((type, msg) => totalMessages.Add((string)msg));

            var hub1Messages = new List<string>();
            var hub2Messages = new List<string>();

            hub1.Subscribe<string>(x => hub1Messages.Add(x));
            hub2.Subscribe<string>(x => hub2Messages.Add(x));

            hub1.Publish("A");

            hub2.Publish("B");
            hub2.Publish("C");

            CollectionAssert.AreEqual(new[] { "A", "B", "C" }, totalMessages);
            CollectionAssert.AreEqual(new[] { "A" }, hub1Messages);
            CollectionAssert.AreEqual(new[] { "B", "C" }, hub2Messages);
        }
    }
}
