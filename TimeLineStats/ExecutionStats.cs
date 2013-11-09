using Glimpse.Core.Extensibility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StatsdNet.Glimpse.Execution
{
    public class ExecutionStats : TabBase, ITabSetup
    {
        public override object GetData(ITabContext context)
        {
            var results = new Dictionary<string, int>();

            results.Add("Test", 1);

            return results;
        }

        public override string Name
        {
            get { return "Statistics"; }
        }

        public void Setup(ITabSetupContext context)
        {
            // no op currently.
        }

    }
}
