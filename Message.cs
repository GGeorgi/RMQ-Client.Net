using System;
using Newtonsoft.Json;

namespace RMQClient.Net
{
    public class Message<T> : EventArgs
    {
        public T Body { get; set; }
        
        [JsonConverter(typeof(CodeConverter))]
        public IConvertible Code { get; set; }
        public bool Error { get; set; }
    }

    public class Message : EventArgs
    {
        public dynamic Body { get; set; }

        [JsonConverter(typeof(CodeConverter))]
        public IConvertible Code { get; set; }
        public bool Error { get; set; }
    }

    public class CodeConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(int);
        }

        public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue,
            JsonSerializer serializer)
        {
            // Would decimal be more appropriate than double?
            var value = serializer.Deserialize<int?>(reader);
            if (value == null)
                return null;
            return Convert.ToInt32(value.Value);
        }

        public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
        {
            writer.WriteValue(Convert.ToInt32(value));
        }
    }
}