using Glimpse.Core.Message;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StatsdNet.Glimpse.Execution
{
    public class StatsdNetMessage : ITimelineMessage
    {
        public TimelineCategoryItem EventCategory { get; set; }
        public string EventName { get; set; }
        public string EventSubText { get; set; }
        public TimeSpan Duration { get; set; }
        public TimeSpan Offset { get; set; }
        public DateTime StartTime { get; set; }
        public Guid Id { get; set; }
    }
}
