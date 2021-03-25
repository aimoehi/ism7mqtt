﻿using System.Xml.Serialization;

namespace ism7mqtt.ISM7.Protocol
{
    [XmlRoot("iac")]
    public class InfonumberWriteResp
    {
        [XmlAttribute("se")]
        public string Seq { get; set; }

        [XmlAttribute("ba")]
        public string BusAddress { get; set; }

        [XmlAttribute("st")]
        public TelegrResponseState State { get; set; }

        [XmlAttribute("in")]
        public ushort InfoNumber { get; set; }

        [XmlAttribute("dl")]
        public string DBLow { get; set; }

        [XmlAttribute("dh")]
        public string DBHigh { get; set; }
    }
}