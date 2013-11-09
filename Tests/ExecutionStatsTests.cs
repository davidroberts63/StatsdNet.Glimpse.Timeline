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
using StatsdNet.Glimpse.Execution;

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

        public ExecutionStats StatsTab { get; set; }
        public Mock<IStatsdPipe> MockStatsdPipe { get; set; }
        public Mock<ITimelineMessage> MockMessage { get; set; }
        public Mock<ISourceMessage> MockSourceMessage { get; set; }

        public void Given_the_tab_with_mock_statsdpipe()
        {
            MockStatsdPipe = new Mock<IStatsdPipe>();
            StatsTab = new ExecutionStats(MockStatsdPipe.Object);
        }

        public void And_message_with(string name, long milliseconds)
        {
            MockMessage = new Mock<ITimelineMessage>();
            MockMessage.SetupAllProperties();

            MockMessage.Object.EventName = name;
            MockMessage.Object.EventSubText = "subtext";
            MockMessage.Object.Duration = TimeSpan.FromMilliseconds(milliseconds);
        }

        public void And_SourceMessage()
        {
            MockSourceMessage = new Mock<ISourceMessage>();
            var mockTimeline = MockSourceMessage.As<ITimelineMessage>();
            MockSourceMessage.SetupAllProperties();

            MockSourceMessage.Object.ExecutedMethod = typeof(ExecutionStatsTests).GetMethod("Given_the_tab_with_mock_statsdpipe");
            MockSourceMessage.Object.ExecutedType = typeof(ExecutionStatsTests);

            mockTimeline.SetupAllProperties();
            mockTimeline.Object.Duration = TimeSpan.FromMilliseconds(5);
        }

        [Test]
        public void SendMessageStats_sends_message_timing()
        {
            Given_the_tab_with_mock_statsdpipe();
            And_message_with("MethodOneName", 5);

            StatsTab.SendMessageStats(MockMessage.Object);

            MockStatsdPipe.Verify(s => s.Timing("MethodOneName(subtext)", 5, 1));
        }

        [Test]
        public void SendMessageStatus_does_not_use_subtext_if_empty()
        {
            Given_the_tab_with_mock_statsdpipe();
            And_message_with("MethodOneName", 5);
            MockMessage.Object.EventSubText = String.Empty;

            StatsTab.SendMessageStats(MockMessage.Object);

            MockStatsdPipe.Verify(s => s.Timing("MethodOneName", 5, 1));
        }

        [Test]
        public void SendMessageStatus_uses_MethodInfo_when_message_is_SourceMessage()
        {
            Given_the_tab_with_mock_statsdpipe();
            And_SourceMessage();
            
            StatsTab.SendMessageStats(MockSourceMessage.Object as ITimelineMessage);

            string name = "Tests.ExecutionStatsTests.Given_the_tab_with_mock_statsdpipe";
            MockStatsdPipe.Verify(s => s.Timing(name, 5, 1));
        }

        [Test]
        public void GetData_returns_stats_counts()
        {
            Given_the_tab_with_mock_statsdpipe();

            And_message_with("MethodTwoName", 2);
            StatsTab.SendMessageStats(MockMessage.Object);

            And_message_with("MethodTwoName", 7);
            StatsTab.SendMessageStats(MockMessage.Object);

            And_message_with("DifferentMethodName", 1);
            StatsTab.SendMessageStats(MockMessage.Object);

            And_message_with("DifferentMethodName", 4);
            StatsTab.SendMessageStats(MockMessage.Object);

            And_message_with("DifferentMethodName", 9);
            StatsTab.SendMessageStats(MockMessage.Object);

            Dictionary<string, string> results = StatsTab.GetData(null) as Dictionary<string, string>;

            Assert.Contains("MethodTwoName(subtext)", results.Keys);
            Assert.Contains("DifferentMethodName(subtext)", results.Keys);
            Assert.AreEqual("2", results["MethodTwoName(subtext)"]);
            Assert.AreEqual("3", results["DifferentMethodName(subtext)"]);
        }

        [Test]
        public void GetData_returns_pipe_status_when_not_active()
        {
            Given_the_tab_with_mock_statsdpipe();

            Dictionary<string, string> results = StatsTab.GetData(null) as Dictionary<string, string>;
            
            Assert.Contains("StatsdNet.Active", results.Keys);
            Assert.Contains("StatsdNet.ApplicationName", results.Keys);
            Assert.Contains("StatsdNet.Server", results.Keys);

            Assert.AreEqual(false.ToString(), results["StatsdNet.Active"]);
            Assert.AreEqual("Unknown", results["StatsdNet.ApplicationName"]);
            Assert.AreEqual("Unknown", results["StatsdNet.Server"]);
        }

        [Test]
        public void Default_ctor_provides_concrete_StatsdNetPipe()
        {
            var tab = new TestExecutionStats();

            Assert.IsInstanceOf<StatsdPipe>(tab.WrappedStatsdPipe);
        }

        public class TestExecutionStats : ExecutionStats
        {
            public IStatsdPipe WrappedStatsdPipe
            {
                get
                {
                    return this.StatsdPipe;
                }
            }
        }
    }
}
