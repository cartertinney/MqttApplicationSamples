﻿
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Client.Extensions;

//System.Diagnostics.Trace.Listeners.Add(new System.Diagnostics.ConsoleTraceListener());

var cs = MqttConnectionSettings.CreateFromEnvVars();
Console.WriteLine($"Connecting to {cs}");

var mqttClient = new MqttFactory().CreateMqttClient(MqttNetTraceLogger.CreateTraceLogger());

var connAck = await mqttClient!.ConnectAsync(new MqttClientOptionsBuilder().WithConnectionSettings(cs).Build());
Console.WriteLine($"Client Connected: {mqttClient.IsConnected} with CONNACK: {connAck.ResultCode}");

mqttClient.ApplicationMessageReceivedAsync += async m => await Console.Out.WriteAsync(
    $"Received message on topic: '{m.ApplicationMessage.Topic}' with content: '{m.ApplicationMessage.ConvertPayloadToString()}'\n\n");

var suback = await mqttClient.SubscribeAsync("sample/+", MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce);
suback.Items.ToList().ForEach(s => Console.WriteLine($"subscribed to '{s.TopicFilter.Topic}'  with '{s.ResultCode}'"));

var puback = await mqttClient.PublishStringAsync("sample/topic1", "hello world!", MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce);
Console.WriteLine(puback.ReasonString);

Console.ReadLine();

