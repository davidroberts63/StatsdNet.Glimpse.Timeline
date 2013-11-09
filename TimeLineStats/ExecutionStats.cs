using Glimpse.Core.Extensibility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Glmps = Glimpse.Core;

namespace StatsdNet.Glimpse.Execution
{
    public class ExecutionStats : TabBase, ITabSetup
    {
        protected IStatsdPipe StatsdPipe { get; private set; }
        protected Dictionary<string, int> StatsCounts { get; private set; }

        public ExecutionStats()
            :this(new StatsdPipe())
        {
        }

        public ExecutionStats(IStatsdPipe statsdPipe)
        {
            StatsdPipe = statsdPipe;
            StatsCounts = new Dictionary<string, int>();
        }

        public override object GetData(ITabContext context)
        {
            var data = new Dictionary<string, string>(
                StatsCounts.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToString()));

            data["StatsdNet.Active"] = StatsdPipe.Active.ToString();
            data["StatsdNet.ApplicationName"] = StatsdPipe.ApplicationName ?? "Unknown";
            data["StatsdNet.Server"] = StatsdPipe.Server == null ? "Unknown" : StatsdPipe.Server.ToString();

            return data;
        }

        public override string Name
        {
            get { return "Statistics"; }
        }

        public void Setup(ITabSetupContext context)
        {
            context.MessageBroker.Subscribe<Glmps.Message.ITimelineMessage>(SendMessageStats);
        }

        public void SendMessageStats(Glmps.Message.ITimelineMessage message)
        {
            string statKey = GenerateStatKey(message);

            StatsdPipe.Timing(statKey, (long)message.Duration.TotalMilliseconds);

            if (StatsCounts.ContainsKey(statKey) == false)
            {
                StatsCounts[statKey] = 0;
            }
            StatsCounts[statKey]++;
        }

        protected string GenerateStatKey(Glmps.Message.ITimelineMessage message)
        {
            if (message is Glmps.Message.ISourceMessage)
            {
                var source = message as Glmps.Message.ISourceMessage;

                return source.ExecutedType.FullName + "." + source.ExecutedMethod.Name;
            }
            else
            {
                string subtext = string.IsNullOrWhiteSpace(message.EventSubText) ? "" : "(" + message.EventSubText + ")";
                return message.EventName + subtext;
            }
        }

    }
}
