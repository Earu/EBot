﻿using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;

namespace Energize.Services.Commands.Social
{
    [DataContract]
    public class LoveObject
    {
        [DataMember]
        public string percentage;

        [DataMember]
        public string result;
    }
}