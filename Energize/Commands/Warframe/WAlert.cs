﻿using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;

namespace Energize.Commands.Warframe
{
    [DataContract]
    public class WAlert
    {
        //_id
        [DataMember]
        public WDateHolder Activation;
        [DataMember]
        public WDateHolder Expiry;
        [DataMember]
        public WMission MissionInfo;
    }
}