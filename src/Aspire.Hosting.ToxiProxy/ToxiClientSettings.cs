using System.Text.Json;
using System.Text.Json.Serialization;
using Refit;

namespace Aspire.Hosting.ToxiProxy;

public static class ToxiClientSettings
{
    public static JsonSerializerOptions JsonSerializerOptions { get; } = CreateJsonSerializerOptions();

    public static RefitSettings Refit { get; } = new()
    {
        ContentSerializer = new SystemTextJsonContentSerializer(JsonSerializerOptions),
    };

    public static JsonSerializerOptions CreateJsonSerializerOptions()
    {
        var options = new JsonSerializerOptions();
        options.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
