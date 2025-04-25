using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using Microsoft.OpenApi.Any;
using System.Linq;
using System.Reflection;
using System.ComponentModel;
using SharedServices.Models; // Add using for DefaultValueAttribute

namespace CrystalWebApp
{
    public class RequestBodyDefaultValueFilter : IOperationFilter
    {
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            // Check if the operation has a request body
            if (operation.RequestBody == null)
                return;

            // Iterate through the request body content types (e.g., application/json)
            foreach (var content in operation.RequestBody.Content)
            {
                // Check if the schema references our FilterSelection model
                // Using EndsWith because the reference ID might be fully qualified
                if (content.Value.Schema?.Reference?.Id?.EndsWith(nameof(FilterSelection)) ?? false)
                {
                    // Create an example OpenApiObject for the FilterSelection
                    var exampleObject = new OpenApiObject();
                    
                    // Specific default example for the 'Lines' property
                    var linesExample = new OpenApiArray
                    {
                        new OpenApiString("001"),
                        new OpenApiString("002"),
                        new OpenApiString("003"),
                        new OpenApiString("004"),
                        new OpenApiString("005"),
                        new OpenApiString("006")
                    };
                    
                    // Generic empty list example for other lists
                    var emptyListExample = new OpenApiArray();

                    // Populate the example object using reflection and DefaultValue attributes
                    var properties = typeof(FilterSelection).GetProperties();
                    foreach (var prop in properties)
                    {
                        // Convert C# property name (PascalCase) to JSON property name (camelCase)
                        var propNameCamelCase = LowercaseFirstLetter(prop.Name);
                        
                        // Special case for Lines
                        if (prop.Name == nameof(FilterSelection.Lines))
                        {
                            exampleObject.Add(propNameCamelCase, linesExample);
                        }
                        // Handle other List<> types
                        else if (prop.PropertyType.IsGenericType && prop.PropertyType.GetGenericTypeDefinition() == typeof(List<>))
                        {
                            // Check if it's List<int> or List<string> etc. if necessary for different empty examples
                            exampleObject.Add(propNameCamelCase, emptyListExample);
                        }
                        // Handle properties with [DefaultValue] attribute
                        else if (prop.GetCustomAttribute<DefaultValueAttribute>() is var attr && attr != null)
                        {
                             if (attr.Value is string strValue)
                             {
                                exampleObject.Add(propNameCamelCase, new OpenApiString(strValue));
                             }
                             else if (attr.Value is int intValue)
                             {
                                 exampleObject.Add(propNameCamelCase, new OpenApiInteger(intValue));
                             }
                             else if (attr.Value is bool boolValue)
                             {
                                 exampleObject.Add(propNameCamelCase, new OpenApiBoolean(boolValue));
                             }
                             // Fallback for other types - might need adjustment
                             else if (attr.Value != null)
                             {
                                 // Try converting to OpenApi type based on actual value type
                                 var openApiValue = CreateOpenApiPrimitive(attr.Value);
                                 if (openApiValue != null)
                                 {
                                     exampleObject.Add(propNameCamelCase, openApiValue);
                                 }
                                 else // Fallback to string if conversion unknown
                                 {
                                      exampleObject.Add(propNameCamelCase, new OpenApiString(attr.Value.ToString()));
                                 }
                             }
                        }
                        // Default for properties without DefaultValue (e.g., nullable strings -> empty string)
                        else if (prop.PropertyType == typeof(string))
                        {
                             exampleObject.Add(propNameCamelCase, new OpenApiString(""));
                        }
                        // Add more defaults if necessary for other types without DefaultValue
                        // else if (prop.PropertyType == typeof(int?)) { exampleObject.Add(propNameCamelCase, new OpenApiInteger(0)); }
                    }
                    
                    // Set the example for this content type (e.g., application/json)
                    content.Value.Example = exampleObject;
                }
            }
        }

        // Helper to convert property names to camelCase for JSON
        private static string LowercaseFirstLetter(string s)
        {
            if (string.IsNullOrEmpty(s) || char.IsLower(s[0]))
                return s;
            return char.ToLowerInvariant(s[0]) + s.Substring(1);
        }

        // Helper to create OpenApi primitive types from object values
        private static IOpenApiPrimitive CreateOpenApiPrimitive(object value)
        {
            return value switch
            {
                string s => new OpenApiString(s),
                int i => new OpenApiInteger(i),
                long l => new OpenApiLong(l),
                float f => new OpenApiFloat(f),
                double d => new OpenApiDouble(d),
                decimal dec => new OpenApiDouble((double)dec), // Note: OpenAPI doesn't have decimal
                bool b => new OpenApiBoolean(b),
                DateTime dt => new OpenApiDateTime(dt),
                DateTimeOffset dto => new OpenApiDateTime(dto),
                Guid guid => new OpenApiString(guid.ToString()),
                byte b => new OpenApiByte(b),
                 byte[] bytes => new OpenApiBinary(bytes),
                _ => null // Unknown type
            };
        }
    }
} 