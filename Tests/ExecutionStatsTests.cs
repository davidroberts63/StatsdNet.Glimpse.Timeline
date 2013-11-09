using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Moq;
using Glimpse.Core.Extensibility;
using Glimpse.Core.Message;
using NUnit.Framework;

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
    }
}
