using Microsoft.OpenApi;
using System.Text.Json.Nodes;
using RelaxKon.Auth.Application.Chat;
namespace RelaxKon.Auth.Configuration;

public static class ApiDocumentation
{
    public static IServiceCollection AddApiDocumentation(this IServiceCollection services)
    {
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo { Title = "RelaxKon Server API", Version = "v1" });
            options.MapType<ChatContent>(() => new OpenApiSchema
            {
                OneOf = [new OpenApiSchema { Type = JsonSchemaType.String },
                    new OpenApiSchema { Type = JsonSchemaType.Array, Items = new OpenApiSchema
                    {
                        OneOf = [new OpenApiSchema
                        {
                            Type = JsonSchemaType.Object, Required = new HashSet<string> { "type", "text" },
                            Properties = new Dictionary<string, IOpenApiSchema>
                            {
                                ["type"] = new OpenApiSchema { Type = JsonSchemaType.String, Enum = [JsonValue.Create("text")] },
                                ["text"] = new OpenApiSchema { Type = JsonSchemaType.String }
                            }
                        }, new OpenApiSchema
                        {
                            Type = JsonSchemaType.Object, Required = new HashSet<string> { "type", "image_url" },
                            Properties = new Dictionary<string, IOpenApiSchema>
                            {
                                ["type"] = new OpenApiSchema { Type = JsonSchemaType.String, Enum = [JsonValue.Create("image_url")] },
                                ["image_url"] = new OpenApiSchema
                                {
                                    Type = JsonSchemaType.Object, Required = new HashSet<string> { "url" },
                                    Properties = new Dictionary<string, IOpenApiSchema>
                                    { ["url"] = new OpenApiSchema { Type = JsonSchemaType.String, Description = "HTTP(S) URL or image base64 data URL" } }
                                }
                            }
                        }]
                    }}]
            });
        });
        services.AddSwaggerGenNewtonsoftSupport();
        return services;
    }
}
