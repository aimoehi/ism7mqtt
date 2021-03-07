﻿using System;
using Newtonsoft.Json.Linq;

namespace ism7mqtt.ISM7.Xml
{
    public class NumericConverter16Template:SingleTelegramConverterTemplateBase
    {
        private ushort? _value;

        protected override void AddTelegram(byte low, byte high)
        {
            _value = (ushort) ((high << 8) | low);
        }

        public override bool HasValue => _value.HasValue;

        public override JValue GetValue()
        {
            if (!HasValue)
                throw new InvalidOperationException();
            JValue result;
            switch (Type)
            {
                case "US":
                    result = new JValue(_value.Value);
                    _value = null;
                    return result;
                case "SS":
                    result = new JValue((short)_value.Value);
                    _value = null;
                    return result;
                case "SS10":
                    result = new JValue(((short)_value.Value) / 10.0);
                    _value = null;
                    return result;
                case "US10":
                    result = new JValue(_value.Value / 10.0);
                    _value = null;
                    return result;
                case "SS100":
                    result = new JValue(((short)_value.Value) / 100.0);
                    _value = null;
                    return result;
                case "SSPR":
                    result = new JValue(((short) _value.Value) * (1.0 / 255));
                    _value = null;
                    return result;
                case "US4":
                    result = new JValue(_value.Value / 4.0);
                    _value = null;
                    return result;
                default:
                    throw new NotImplementedException($"type '{Type}' for CTID '{CTID}' is not yet implemented");
            }
        }
    }
}