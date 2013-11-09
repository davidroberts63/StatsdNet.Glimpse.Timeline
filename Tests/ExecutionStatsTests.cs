using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Moq;
using Glimpse.Core.Extensibility;
using Glimpse.Core.Message;
using NUnit.Framework;
using StatsdNet;

namespace Tests
{
    [TestFixture]
    public class ExecutionStatsTests
    {
        [Test]
        public void Setup_subscribes_to_Timeline_Messages()
        {
            var context = new Mock<ITabSetupContext>();
            var broker = new Mock<IMessageBroker>();
            context.Setup(c => c.MessageBroker).Returns(broker.Object);

            var tab = new StatsdNet.Glimpse.Execution.ExecutionStats();

            tab.Setup(context.Object);

            broker.Verify(b => b.Subscribe<ITimelineMessage>(It.IsAny<Action<ITimelineMessage>>()));
        }

        [Test]
        public void SendMessageStats_sends_message_timing()
        {
            var message = new Mock<ITimelineMessage>();
            message.SetupAllProperties();
            message.Object.EventName = "MethodOneName";
            message.Object.Duration = TimeSpan.FromMilliseconds(5.25);

            var statsd = new Mock<IStatsdPipe>();

            var tab = new StatsdNet.Glimpse.Execution.ExecutionStats(statsd.Object);

            tab.SendMessageStats(message.Object);

            statsd.Verify(s => s.Timing("MethodOneName", 5, 1));
        }
    }
}
