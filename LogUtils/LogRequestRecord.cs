﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LogUtils
{
    public record struct LogRequestRecord(RejectionReason Reason, BetaLogger Source)
    {
        public bool Rejected => Reason != RejectionReason.None;
    }
}
