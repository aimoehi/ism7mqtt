﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using ism7mqtt.ISM7.Xml;

namespace ism7mqtt.HomeAssistant
{
    public class HaDiscovery
    {
        private readonly Ism7Config _config;

        public HaDiscovery(Ism7Config config)
        {
            _config = config;
        }

        public IEnumerable<JsonMessage> GetDiscoveryInfo(string discoveryId)
        {
            return _config.Devices.SelectMany(x => GetDiscoveryInfo(discoveryId, x));
        }

        private IEnumerable<JsonMessage> GetDiscoveryInfo(string discoveryId, Ism7Config.RunningDevice device)
        {
            List<JsonMessage> result = new List<JsonMessage>();
            foreach (var parameter in device.Parameters)
            {
                var descriptor = parameter.Descriptor;
                var type = GetHomeAssistantType(descriptor);
                if (type == null) continue;
                if (descriptor.ControlType == "DaySwitchTimes" || descriptor.ControlType.Contains("NO_DISPLAY")) continue;

                // Handle parameters with name-duplicates - their ID will be part of the topic
                string deduplicator = "";
                string deduplicatorLabel = "";
                if (parameter.IsDuplicate)
                {
                    deduplicator = $"/{descriptor.PTID}";
                    deduplicatorLabel = $"_{descriptor.PTID}";
                }
                
                var discoveryName = parameter.MqttName;
                string uniqueId = $"{discoveryId}_{discoveryName}{deduplicatorLabel}";
                string discoveryTopic = $"homeassistant/{type}/{uniqueId}/config";
                var message = new JsonObject();
                message.Add("unique_id", uniqueId);

                var discoveryTopicSSuffix = GetDiscoveryTopicSuffix(descriptor);
                string stateTopic = $"{device.MqttTopic}/{discoveryName}{deduplicator}{discoveryTopicSSuffix}";
                message.Add("state_topic", stateTopic);

                if (descriptor.IsWritable)
                {
                    string commandTopic = $"{device.MqttTopic}/set/{parameter.MqttName}{deduplicator}{discoveryTopicSSuffix}";
                    message.Add("command_topic", commandTopic);
                }

                message.Add("name", descriptor.Name);
                message.Add("object_id", $"{discoveryId}_{device.Name}_{descriptor.Name}{deduplicatorLabel}");

                foreach (var (key, value) in GetDiscoveryProperties(descriptor))
                {
                    message.Add(key, value);
                }

                // Try to guess a suitable icon if none given
                if (!message.ContainsKey("icon"))
                {
                    if (descriptor.Name.ToLower().Contains("brenner"))
                        message.Add("icon", "mdi:fire");
                    else if (descriptor.Name.ToLower().Contains("solar"))
                        message.Add("icon", "mdi:solar-panel");
                    else if (descriptor.Name.ToLower().Contains("ventil"))
                        message.Add("icon", "mdi:pipe-valve");
                    else if (descriptor.Name.ToLower().Contains("heizung"))
                        message.Add("icon", "mdi:radiator");
                    else if (descriptor.Name.ToLower().Contains("pumpe"))
                        message.Add("icon", "mdi:pump");
                    if (descriptor.Name.ToLower().Contains("druck"))
                        message.Add("icon", "mdi:gauge");
                }

                message.Add("device", GetDiscoveryDeviceInfo(discoveryId, device));
                result.Add(new JsonMessage(discoveryTopic, message));
            }
            return result;
        }

        private JsonObject GetDiscoveryDeviceInfo(string discoveryId, Ism7Config.RunningDevice device)
        {
            return new JsonObject
            {
                { "configuration_url", $"http://{device.IP}/" },
                { "manufacturer", "Wolf" },
                { "model", device.Name },
                { "name", $"{discoveryId} {device.Name}" },
                {
                    "connections", new JsonArray
                    {
                        new JsonArray
                        {
                            "ip_dev",
                            $"{device.IP}_{device.Name}"
                        }
                    }
                }
            };
        }

        private string GetDiscoveryTopicSuffix(ParameterDescriptor descriptor)
        {
            if (descriptor is ListParameterDescriptor list)
            {
                if (!list.IsWritable && list.IsBoolean)
                {
                    return String.Empty;
                }
                return "/text";
            }
            else
            {
                return String.Empty;
            }
        }

        private string GetHomeAssistantType(ParameterDescriptor descriptor)
        {
            switch (descriptor)
            {
                case ListParameterDescriptor list:
                    // Try to auto-detect switch and select vs sensor and binary_sensor
                    if (list.IsWritable)
                    {
                        return list.IsBoolean ? "switch" : "select";
                    }

                    return list.IsBoolean ? "binary_sensor" : "sensor";
                case NumericParameterDescriptor numeric:
                    return numeric.IsWritable ? "number" : "sensor";
                case TextParameterDescriptor text:
                    return text.IsWritable ? "text" : "sensor";
                case OtherParameterDescriptor other:
                    return other.IsWritable ? "text" : "sensor";
                default:
                    return null;
            }
        }

        private IEnumerable<(string, JsonNode)> GetDiscoveryProperties(ParameterDescriptor descriptor)
        {
            switch (descriptor)
            {
                case NumericParameterDescriptor numeric:
                    if (numeric.IsWritable)
                    {
                        if (numeric.MinValueCondition != null)
                            yield return("min", Double.Parse(numeric.MinValueCondition));
                        if (numeric.MaxValueCondition != null)
                            yield return ("max", Double.Parse(numeric.MaxValueCondition));
                        if (numeric.StepWidth != null)
                            yield return ("step", numeric.StepWidth);
                    }
                    if (numeric.UnitName != null)
                    {
                        yield return ("unit_of_measurement", numeric.UnitName);
                        if (numeric.UnitName == "°C")
                        {
                            yield return ("icon", "mdi:thermometer");
                            yield return ("state_class", "measurement");
                        }
                        else if (numeric.UnitName == "%")
                        {
                            yield return ("state_class", "measurement");
                        }
                    }
                    break;
                case ListParameterDescriptor list:
                    if (list.IsWritable)
                    {
                        if (list.IsBoolean)
                        {
                            if (list.Options.Any(x => x.Value == "Ein"))
                            {
                                yield return("payload_on", "Ein");
                            }
                            if (list.Options.Any(x => x.Value == "Aktiviert"))
                            {
                                yield return("payload_on", "Aktiviert");
                            }
                            if (list.Options.Any(x => x.Value == "Aus"))
                            {
                                yield return("payload_off", "Aus");
                            }
                            if (list.Options.Any(x => x.Value == "Deaktiviert"))
                            {
                                yield return("payload_off", "Deaktiviert");
                            }
                        }
                        else
                        {
                            var options = new JsonArray();
                            foreach (var value in list.Options)
                            {
                                options.Add(value.Value);
                            }
                            yield return("options", options);

                        }
                    }
                    else if (list.IsBoolean)
                    {
                        yield return("payload_on", "true");
                        yield return("payload_off", "false");
                    }
                    break;
                default:
                    yield break;
            }
        }
    }
}
