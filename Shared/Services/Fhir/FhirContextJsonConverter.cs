#region (c) 2024 Joseph Shook. All rights reserved.
// /*
//  Authors:
//     Joseph Shook   Joseph.Shook@Surescripts.com
// 
//  See LICENSE in the project root for license information.
// */
#endregion

using System.Text.Json;
using System.Text.Json.Serialization;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;

namespace UdapEd.Shared.Services.Fhir;
public class FhirContextJsonConverter : JsonConverter<FhirContext>
{
    private readonly bool _indent;

    public FhirContextJsonConverter(bool indent = false)
    {
        _indent = indent;
    }

    public override FhirContext? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("Expected StartObject token");
        }

        var fhirContext = new FhirContext();
        var fhirJsonParser = new FhirJsonParser();

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                return fhirContext;
            }

            if (reader.TokenType == JsonTokenType.PropertyName)
            {
                string? propertyName = reader.GetString();
                reader.Read();

                switch (propertyName)
                {
                    case nameof(FhirContext.CurrentPatient):
                        fhirContext.CurrentPatient = fhirJsonParser.Parse<Patient>(JsonDocument.ParseValue(ref reader).RootElement.GetRawText());
                        break;
                    case nameof(FhirContext.CurrentRelatedPerson):
                        fhirContext.CurrentRelatedPerson = fhirJsonParser.Parse<RelatedPerson>(JsonDocument.ParseValue(ref reader).RootElement.GetRawText());
                        break;
                    case nameof(FhirContext.CurrentPerson):
                        fhirContext.CurrentPerson = fhirJsonParser.Parse<Person>(JsonDocument.ParseValue(ref reader).RootElement.GetRawText());
                        break;
                    default:
                        throw new JsonException($"Unknown property: {propertyName}");
                }
            }
        }

        throw new JsonException("Expected EndObject token");
    }

    public override void Write(Utf8JsonWriter writer, FhirContext value, JsonSerializerOptions options)
    {
        var fhirJsonSerializer = new FhirJsonSerializer(new SerializerSettings { Pretty = _indent });

        writer.WriteStartObject();

        if (value.CurrentPatient != null)
        {
            writer.WritePropertyName(nameof(FhirContext.CurrentPatient));
            var patientJson = fhirJsonSerializer.SerializeToString(value.CurrentPatient);
            writer.WriteRawValue(patientJson);
        }

        if (value.CurrentRelatedPerson != null)
        {
            writer.WritePropertyName(nameof(FhirContext.CurrentRelatedPerson));
            var relatedPersonJson = fhirJsonSerializer.SerializeToString(value.CurrentRelatedPerson);
            writer.WriteRawValue(relatedPersonJson);
        }

        if (value.CurrentPerson != null)
        {
            writer.WritePropertyName(nameof(FhirContext.CurrentPerson));
            var personJson = fhirJsonSerializer.SerializeToString(value.CurrentPerson);
            writer.WriteRawValue(personJson);
        }

        writer.WriteEndObject();
    }
}
