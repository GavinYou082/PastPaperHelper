﻿using Prism.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PastPaperHelper.Events
{
    public class MessageBarEnqueuedEvent : PubSubEvent<string> { }
    public class SubjectListDownloadedEvent : PubSubEvent { }
    public class UpdateServiceErrorEvent : PubSubEvent { }
}
