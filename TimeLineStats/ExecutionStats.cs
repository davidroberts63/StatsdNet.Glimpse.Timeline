using Glimpse.Core.Extensibility;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Glmps = Glimpse.Core;

namespace StatsdNet.Glimpse.Execution
{
    public class ExecutionStats : TabBase, ITabSetup
    {
        protected IStatsdPipe StatsdPipe { get; private set; }
        protected string StatsCountsKey { get { return typeof(ExecutionStats).FullName; } }

        public ExecutionStats()
            :this(new StatsdPipe())
        {
        }

        public ExecutionStats(IStatsdPipe statsdPipe)
        {
            StatsdPipe = statsdPipe;
        }

        public override object GetData(ITabContext context)
        {
            SetupStatCountsStore(context.TabStore);

            var counts = context.TabStore.Get(StatsCountsKey) as Dictionary<string, double>;
            var data = new Dictionary<string, string>(
                counts.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToString()));

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
            context.MessageBroker.Subscribe<Glmps.Message.ITimelineMessage>(message => SendMessageStats(message, context));
        }

        public void SendMessageStats(Glmps.Message.ITimelineMessage message, ITabSetupContext context = null)
        {
            var selfTimer = Stopwatch.StartNew();

            string statKey = GenerateStatKey(message);

            StatsdPipe.Timing(statKey, (long)message.Duration.TotalMilliseconds);

            IncrementStoredCount(statKey, context);

            selfTimer.Stop();

            IncrementStoredCount("StatsdNet.TimingSum", context, selfTimer.Elapsed.TotalMilliseconds);
        }

        protected void IncrementStoredCount(string statKey, ITabSetupContext context, double amount = 1)
        {
            SetupStatCountsStore(context.GetTabStore());
            var counts = context.GetTabStore().Get(StatsCountsKey) as Dictionary<string, double>;

            if (counts.ContainsKey(statKey) == false)
            {
                counts[statKey] = 0;
            }

            counts[statKey] += amount;
        }

        protected void SetupStatCountsStore(IDataStore store)
        {
            if (store.Contains(StatsCountsKey) == false)
            {
                store.Set(StatsCountsKey, new Dictionary<string, double>());
            }
        }

        protected string GenerateStatKey(Glmps.Message.ITimelineMessage message)
        {
            string key = String.Empty;

            if (message is Glmps.Message.ISourceMessage)
            {
                var source = message as Glmps.Message.ISourceMessage;
                key = source.ExecutedType.FullName + "." + source.ExecutedMethod.Name;
            }
            else
            {
                string subtext = string.IsNullOrWhiteSpace(message.EventSubText) ? "" : "(" + message.EventSubText + ")";
                key = message.EventName + subtext;

                key = key.Replace(".", "-");
                key = key.Replace(" ", "_");
            }

            return key;
        }

    }
}
