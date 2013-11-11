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
        public Mock<ITabSetupContext> MockTabSetupContext { get; set; }
        public Mock<ITabContext> MockTabContext { get; set; }
        public Mock<IMessageBroker> MockMessageBroker { get; set; }
        public IDataStore DataStore { get; set; }

        public void Given_the_tab_with_mock_statsdpipe()
        {
            MockStatsdPipe = new Mock<IStatsdPipe>();
            StatsTab = new ExecutionStats(MockStatsdPipe.Object);
            MockTabContext = new Mock<ITabContext>();
            MockTabSetupContext = new Mock<ITabSetupContext>();
            DataStore = new DictionaryDataStoreAdapter(new Dictionary<object, object>());
            MockMessageBroker = new Mock<IMessageBroker>();

            MockTabContext.Setup(ctx => ctx.TabStore).Returns(DataStore);
            MockTabSetupContext.Setup(ctx => ctx.GetTabStore()).Returns(DataStore);
            MockTabSetupContext.Setup(ctx => ctx.MessageBroker).Returns(MockMessageBroker.Object);

        }

        public void And_message_with(string name, long milliseconds)
        {
            MockMessage = new Mock<ITimelineMessage>();
            MockMessage.SetupAllProperties();

            MockMessage.Object.EventName = name;
            MockMessage.Object.EventSubText = "subtext";
            MockMessage.Object.Duration = TimeSpan.FromMilliseconds(milliseconds);
            MockMessage.Object.Offset = TimeSpan.FromSeconds(3);
            MockMessage.Object.StartTime = DateTime.Now.AddSeconds(-5);
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

            StatsTab.SendMessageStats(MockMessage.Object, MockTabSetupContext.Object);

            MockStatsdPipe.Verify(s => s.Timing("MethodOneName_subtext", 5, 1));
        }

        [Test]
        public void SendMessageStatus_does_not_use_subtext_if_empty()
        {
            Given_the_tab_with_mock_statsdpipe();
            And_message_with("MethodOneName", 5);
            MockMessage.Object.EventSubText = String.Empty;

            StatsTab.SendMessageStats(MockMessage.Object, MockTabSetupContext.Object);

            MockStatsdPipe.Verify(s => s.Timing("MethodOneName", 5, 1));
        }

        [Test]
        public void SendMessageStatus_cleans_periods_from_key_when_using_EventName()
        {
            Given_the_tab_with_mock_statsdpipe();
            And_message_with("Method.One.Name", 5);

            StatsTab.SendMessageStats(MockMessage.Object, MockTabSetupContext.Object);

            MockStatsdPipe.Verify(s => s.Timing("Method-One-Name_subtext", 5, 1));
        }

        [Test]
        public void SendMessageStatus_cleans_spaces_from_key()
        {
            Given_the_tab_with_mock_statsdpipe();
            And_message_with("Method One Name", 5);

            StatsTab.SendMessageStats(MockMessage.Object, MockTabSetupContext.Object);

            MockStatsdPipe.Verify(s => s.Timing("Method_One_Name_subtext", 5, 1));
        }

        [Test]
        public void SendMessageStatus_sums_stats_timing()
        {
            Given_the_tab_with_mock_statsdpipe();
            And_message_with("MethodOneName", 5);

            for (int i = 0; i < 100; i++)
            {
                StatsTab.SendMessageStats(MockMessage.Object, MockTabSetupContext.Object);
            }
            var results = StatsTab.GetData(MockTabContext.Object) as Dictionary<string, string>;

            Assert.Contains("StatsdNet.TimingSum", results.Keys);
            Assert.Greater(Convert.ToDouble(results["StatsdNet.TimingSum"]), 0);
        }

        [Test]
        public void SendMessageStatus_sum_doesnt_take_exorbant_amount_of_time()
        {
            Given_the_tab_with_mock_statsdpipe();
            And_message_with("MethodOneName", 5);

            for (int i = 0; i < 100; i++)
            {
                StatsTab.SendMessageStats(MockMessage.Object, MockTabSetupContext.Object);
            }
            var results = StatsTab.GetData(MockTabContext.Object) as Dictionary<string, string>;

            Assert.Contains("StatsdNet.TimingSum", results.Keys);
            Assert.Less(Convert.ToDouble(results["StatsdNet.TimingSum"]), 25); // Roughly 1/4 millisecond per stat being sent.
        }

        [Test]
        public void SendMessageStatus_uses_MethodInfo_when_message_is_SourceMessage()
        {
            Given_the_tab_with_mock_statsdpipe();
            And_SourceMessage();

            StatsTab.SendMessageStats(MockSourceMessage.Object as ITimelineMessage, MockTabSetupContext.Object);

            string name = "Tests.ExecutionStatsTests.Given_the_tab_with_mock_statsdpipe";
            MockStatsdPipe.Verify(s => s.Timing(name, 5, 1));
        }

        [Test]
        public void GetData_returns_stats_counts()
        {
            Given_the_tab_with_mock_statsdpipe();

            And_message_with("MethodTwoName", 2);
            StatsTab.SendMessageStats(MockMessage.Object, MockTabSetupContext.Object);

            And_message_with("MethodTwoName", 7);
            StatsTab.SendMessageStats(MockMessage.Object, MockTabSetupContext.Object);

            And_message_with("DifferentMethodName", 1);
            StatsTab.SendMessageStats(MockMessage.Object, MockTabSetupContext.Object);

            And_message_with("DifferentMethodName", 4);
            StatsTab.SendMessageStats(MockMessage.Object, MockTabSetupContext.Object);

            And_message_with("DifferentMethodName", 9);
            StatsTab.SendMessageStats(MockMessage.Object, MockTabSetupContext.Object);

            Dictionary<string, string> results = StatsTab.GetData(MockTabContext.Object) as Dictionary<string, string>;

            Assert.Contains("MethodTwoName_subtext", results.Keys);
            Assert.Contains("DifferentMethodName_subtext", results.Keys);
            Assert.AreEqual("2", results["MethodTwoName_subtext"]);
            Assert.AreEqual("3", results["DifferentMethodName_subtext"]);
        }

        [Test]
        public void GetData_returns_pipe_status_when_not_active()
        {
            Given_the_tab_with_mock_statsdpipe();

            Dictionary<string, string> results = StatsTab.GetData(MockTabContext.Object) as Dictionary<string, string>;
            
            Assert.Contains("StatsdNet.Active", results.Keys);
            Assert.Contains("StatsdNet.ApplicationName", results.Keys);
            Assert.Contains("StatsdNet.Server", results.Keys);

            Assert.AreEqual(false.ToString(), results["StatsdNet.Active"]);
            Assert.AreEqual("Unknown", results["StatsdNet.ApplicationName"]);
            Assert.AreEqual("Unknown", results["StatsdNet.Server"]);
        }

        [Test]
        public void SendMessageStats_publishes_message_with_duration_of_stats_timing()
        {
            Given_the_tab_with_mock_statsdpipe();
            And_message_with("MethodOneName", 5);

            StatsTab.SendMessageStats(MockMessage.Object, MockTabSetupContext.Object);

            MockMessageBroker.Verify(broker => broker.Publish
                (It.Is<StatsdNetMessage>(msg => msg.EventName.Equals("GlimpseStatsdNetTiming: MethodOneName") && msg.Duration.TotalMilliseconds > 0)));
        }

        [Test]
        public void SendMessageStats_publishes_message_with_offset_of_stats_timing()
        {
            Given_the_tab_with_mock_statsdpipe();
            And_message_with("MethodOneName", 5);

            StatsTab.SendMessageStats(MockMessage.Object, MockTabSetupContext.Object);

            MockMessageBroker.Verify(broker => broker.Publish
                (It.Is<StatsdNetMessage>(msg => msg.Offset == TimeSpan.FromSeconds(8))));
        }

        [Test]
        public void SendMessageStats_ignores_stats_timing_messages()
        {
            Given_the_tab_with_mock_statsdpipe();
            var message = new StatsdNetMessage();

            StatsTab.SendMessageStats(message, MockTabSetupContext.Object);

            MockStatsdPipe.Verify(pipe => pipe.Timing(It.IsAny<string>(), It.IsAny<long>(), 1), Times.Never());
            MockTabSetupContext.Verify(ctx => ctx.GetTabStore(), Times.Never());
            MockMessageBroker.Verify(broker => broker.Publish(It.IsAny<StatsdNetMessage>()), Times.Never);
        }

        [Test]
        public void SendMessageStats_sends_stats_timing_to_statsd()
        {
            Given_the_tab_with_mock_statsdpipe();
            And_message_with("MethodOneName", 5);

            StatsTab.SendMessageStats(MockMessage.Object, MockTabSetupContext.Object);

            MockStatsdPipe.Verify(pipe => pipe.Timing("GlimpseStatsdNetTiming", It.IsAny<long>(), 1));
        }

        [Test]
        public void SendMessageStats_publishes_message_with_statsd_event_category()
        {
            Given_the_tab_with_mock_statsdpipe();
            And_message_with("MethodOneName", 5);

            StatsTab.SendMessageStats(MockMessage.Object, MockTabSetupContext.Object);

            MockMessageBroker.Verify(broker => broker.Publish
                (It.Is<StatsdNetMessage>(msg => msg.EventCategory != null && msg.EventCategory.Name.Equals("StatsdNet"))));
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
