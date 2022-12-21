﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;
using System.Text.Json.Nodes;

namespace ism7mqtt.ISM7.Xml
{
    public class ListParameterDescriptor:ParameterDescriptor
    {
        [XmlElement("App")]
        public bool App { get; set; }

        [XmlElement("MinValueCondition")]
        public string MinValueCondition { get; set; }

        [XmlElement("MaxValueCondition")]
        public string MaxValueCondition { get; set; }

        [XmlElement("KeyValueList")]
        public string KeyValueList { get; set; }

        [XmlElement("DependentDefinitionId")]
        public string DependentDefinitionId { get; set; }

        private IDictionary<string, string> Options {
            get {
                var result = new Dictionary<string, string>();
                if (String.IsNullOrEmpty(KeyValueList))
                    return result;

                var names = KeyValueList.Split(";");
                for (int i = 0; i < names.Length - 1; i += 2) {
                    result.Add(names[i], names[i + 1]);
                }
                return result;
            }
        }

        protected override JsonNode GetValueCore(ConverterTemplateBase converter)
        {
            var value = converter.GetValue();
            var key = value.ToString();
            // some list types have a binary/bool converter, even if it doesn't make much sense.. try to detect those cases
            if (!isBinaryOptions() && converter.GetType() == typeof(BinaryReadOnlyConverterTemplate)) {
                if (value.TryGetValue<bool>(out var valueBool)) {
                    key = valueBool ? "1" : "0";
                }
            }

            var options = Options;
            if (options.ContainsKey(key))
            {
                return new JsonObject
                {
                    ["value"] = value,
                    ["text"] = options[key]
                };
            }
            return value;
        }

        protected override JsonValue GetWrite(JsonNode node)
        {
            if (String.IsNullOrEmpty(KeyValueList)) return base.GetWrite(node);
            if (node is not JsonObject jobject) return base.GetWrite(node);
            if (jobject.TryGetPropertyValue("value", out var value))
            {
                return value.AsValue();
            }
            if (jobject.TryGetPropertyValue("text", out value))
            {
                var names = KeyValueList.Split(';');
                var name = value.ToString();
                for (int i = 1; i < names.Length; i += 2)
                {
                    if (names[i] == name) return JsonValue.Create(names[i - 1]);
                }
            }
            return base.GetWrite(node);
        }

        private bool isBinaryOptions() {
            var opts = Options;
            if (opts.Count != 2)
                return false;
            if (opts.Values.Contains("An") && opts.Values.Contains("Aus"))
                return true;
            if (opts.Values.Contains("Aktiviert") && opts.Values.Contains("Deaktiviert"))
                return true;
            return false;
        }

        private IDictionary<bool, string> getBinaryOptions() {
            if (!isBinaryOptions())
                return null;
            var opts = Options;
            var result = new Dictionary<bool, string>();
            if (opts.Values.Contains("An"))
                result.Add(true, "An");
            else if (opts.Values.Contains("Aktiviert"))
                result.Add(true, "Aktiviert");

            if (opts.Values.Contains("Aus"))
                result.Add(false, "Aus");
            else if (opts.Values.Contains("Deaktiviert"))
                result.Add(false, "Deaktiviert");
            return result;
        }
        
        public override string HomeAssistantType {
            get
            {
                // Try to auto-detect switch and select vs sensor and binary_sensor
                if (IsWritable) {
                    if (isBinaryOptions())
                        return "switch";
                    return "select";
                }

                if (isBinaryOptions()) {
                    return "binary_sensor";
                }
                return "sensor";
            }
        }

        public override IDictionary<string, JsonNode> DiscoveryProperties {
            get
            {
                var properties = new Dictionary<string, JsonNode>();

                if (HomeAssistantType == "select")
                {
                    var options = new JsonArray();
                    foreach (var value in Options.Values)
                        options.Add(value);
                    properties.Add("options", options);
                }
                else if (HomeAssistantType == "switch" || HomeAssistantType == "binary_sensor")
                {
                    var options = getBinaryOptions();
                    properties.Add("payload_on", options[true]);
                    properties.Add("payload_off", options[false]);
                }

                return properties;
            }
        }

        public override string DiscoveryTopicSuffix => "/text";
    }
}