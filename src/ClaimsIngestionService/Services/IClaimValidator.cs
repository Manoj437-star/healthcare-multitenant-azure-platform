using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;
using ClaimsIngestionService.DTOs;

namespace ClaimsIngestionService.Services;

public class ValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
}

public interface IClaimValidator
{
    ValidationResult ValidateClaim(SubmitClaimRequest claim);
}

public class JsonSchemaClaimValidator : IClaimValidator
{
    private readonly JsonSchema _schema;

    public JsonSchemaClaimValidator()
    {
        // JSON Schema defining strict healthcare claim constraints
        var schemaJson = """
        {
          "$schema": "https://json-schema.org/draft/2020-12/schema",
          "title": "HealthcareClaim",
          "type": "object",
          "properties": {
            "claimId": {
              "type": "string",
              "minLength": 3,
              "maxLength": 50
            },
            "tenantId": {
              "type": "string",
              "minLength": 3,
              "maxLength": 50,
              "pattern": "^[a-z0-9-]+$"
            },
            "memberId": {
              "type": "string",
              "minLength": 3,
              "maxLength": 50
            },
            "providerId": {
              "type": "string",
              "minLength": 3,
              "maxLength": 50
            },
            "claimAmount": {
              "type": "number",
              "minimum": 0.01,
              "maximum": 10000000.00
            },
            "claimStatus": {
              "type": "string",
              "enum": ["Submitted", "Pending", "Validated"]
            },
            "serviceDate": {
              "type": "string",
              "pattern": "^[0-9]{4}-[0-9]{2}-[0-9]{2}$"
            },
            "diagnosisCodes": {
              "type": "array",
              "items": { "type": "string", "maxLength": 10 }
            },
            "procedureCodes": {
              "type": "array",
              "items": { "type": "string", "maxLength": 10 }
            }
          },
          "required": ["claimId", "tenantId", "memberId", "providerId", "claimAmount", "serviceDate"]
        }
        """;

        _schema = JsonSchema.FromText(schemaJson);
    }

    public ValidationResult ValidateClaim(SubmitClaimRequest claim)
    {
        var result = new ValidationResult();

        // 1. Validate against JSON Schema
        var jsonString = JsonSerializer.Serialize(claim);
        var jsonNode = JsonNode.Parse(jsonString);

        if (jsonNode == null)
        {
            result.IsValid = false;
            result.Errors.Add("Failed to parse claim payload as JSON.");
            return result;
        }

        var evaluation = _schema.Evaluate(jsonNode, new EvaluationOptions
        {
            OutputFormat = OutputFormat.List
        });

        if (!evaluation.IsValid)
        {
            result.IsValid = false;
            if (evaluation.Details != null)
            {
                foreach (var detail in evaluation.Details)
                {
                    if (detail.Errors != null)
                    {
                        foreach (var err in detail.Errors)
                        {
                            result.Errors.Add($"{detail.InstanceLocation}: {err.Value}");
                        }
                    }
                }
            }

            if (result.Errors.Count == 0)
            {
                result.Errors.Add("Claim payload violates JSON schema requirements.");
            }

            return result;
        }

        // 2. Validate Service Date range (cannot be in the distant future)
        if (DateTime.TryParse(claim.ServiceDate, out var parsedDate))
        {
            if (parsedDate > DateTime.UtcNow.AddDays(1))
            {
                result.IsValid = false;
                result.Errors.Add("ServiceDate cannot be in the future.");
                return result;
            }
        }
        else
        {
            result.IsValid = false;
            result.Errors.Add("ServiceDate must be a valid ISO 8601 date string (YYYY-MM-DD).");
            return result;
        }

        result.IsValid = true;
        return result;
    }
}
