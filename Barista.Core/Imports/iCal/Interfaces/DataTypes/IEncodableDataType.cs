﻿using System;
using System.Collections.Generic;
using System.Text;

namespace Barista.DDay.iCal
{
    public interface IEncodableDataType :
        ICalendarDataType
    {
        string Encoding { get; set; }
    }
}