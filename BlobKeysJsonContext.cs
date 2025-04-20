using System.Text.Json.Serialization;

[JsonSerializable(typeof(Dictionary<string, string>))]
public partial class BlobKeysJsonContext : JsonSerializerContext
{
}