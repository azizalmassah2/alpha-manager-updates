using System.Text.Json.Serialization;

namespace OpenWrtProgrammerPro.Models
{
    public class UbusResponse<T>
    {
        [JsonPropertyName("jsonrpc")]
        public string JsonRpc { get; set; } = "2.0";

        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("result")]
        public T? Result { get; set; }

        [JsonPropertyName("error")]
        public UbusError? Error { get; set; }
    }

    public class UbusError
    {
        [JsonPropertyName("code")]
        public int Code { get; set; }

        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;
    }
}
