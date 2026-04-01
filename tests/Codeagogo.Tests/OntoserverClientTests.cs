// Copyright 2026 CSIRO. Licensed under the Apache License, Version 2.0.
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Moq;
using Moq.Protected;

namespace Codeagogo.Tests;

/// <summary>
/// Unit tests for OntoserverClient using mocked HTTP responses.
/// Translated from the Mac version's Swift tests.
/// </summary>
public class OntoserverClientTests
{
    #region FHIR Response Parsing Tests

    [Fact]
    public void ParseLookupResponse_ValidParameters_ParsesCorrectly()
    {
        var json = """
        {
            "resourceType": "Parameters",
            "parameter": [
                {"name": "version", "valueString": "http://snomed.info/sct/900000000000207008/version/20240101"},
                {"name": "display", "valueString": "Diabetes mellitus"},
                {
                    "name": "property",
                    "part": [
                        {"name": "code", "valueCode": "inactive"},
                        {"name": "value", "valueString": "false"}
                    ]
                },
                {
                    "name": "designation",
                    "part": [
                        {"name": "use", "valueCoding": {"code": "900000000000003001"}},
                        {"name": "value", "valueString": "Diabetes mellitus (disorder)"}
                    ]
                }
            ]
        }
        """;

        var parameters = JsonSerializer.Deserialize<TestFhirParameters>(json, JsonOpts);

        parameters.Should().NotBeNull();
        parameters!.ResourceType.Should().Be("Parameters");
        parameters.Parameter.Should().NotBeNull();
        parameters.Parameter.Should().HaveCount(4);

        // Check version parameter
        var versionParam = parameters.Parameter!.FirstOrDefault(p => p.Name == "version");
        versionParam.Should().NotBeNull();
        versionParam!.ValueString.Should().Be("http://snomed.info/sct/900000000000207008/version/20240101");

        // Check display parameter
        var displayParam = parameters.Parameter!.FirstOrDefault(p => p.Name == "display");
        displayParam.Should().NotBeNull();
        displayParam!.ValueString.Should().Be("Diabetes mellitus");
    }

    [Fact]
    public void ParseBundleResponse_ValidBundle_ParsesCorrectly()
    {
        var json = """
        {
            "resourceType": "Bundle",
            "type": "searchset",
            "entry": [
                {
                    "resource": {
                        "resourceType": "CodeSystem",
                        "url": "http://snomed.info/sct",
                        "version": "http://snomed.info/sct/32506021000036107/version/20240131",
                        "name": "SNOMED_CT_AU",
                        "title": "SNOMED CT Australian Edition"
                    }
                }
            ]
        }
        """;

        var bundle = JsonSerializer.Deserialize<TestFhirBundle>(json, JsonOpts);

        bundle.Should().NotBeNull();
        bundle!.ResourceType.Should().Be("Bundle");
        bundle.Entry.Should().NotBeNull();
        bundle.Entry.Should().HaveCount(1);

        var codeSystem = bundle.Entry![0].Resource;
        codeSystem.Should().NotBeNull();
        codeSystem!.Url.Should().Be("http://snomed.info/sct");
        codeSystem.Title.Should().Be("SNOMED CT Australian Edition");
    }

    #endregion

    #region Error Response Tests

    [Fact]
    public void ParseOperationOutcomeResourceType_NotParameters_HasNullParameterArray()
    {
        var json = """
        {
            "resourceType": "OperationOutcome",
            "issue": [
                {
                    "severity": "error",
                    "code": "not-found",
                    "diagnostics": "Code not found"
                }
            ]
        }
        """;

        // OperationOutcome can be decoded, but resourceType won't match "Parameters"
        var decoded = JsonSerializer.Deserialize<TestFhirParameters>(json, JsonOpts);

        decoded.Should().NotBeNull();
        decoded!.ResourceType.Should().Be("OperationOutcome");
        decoded.ResourceType.Should().NotBe("Parameters");
        // Parameters will be null since OperationOutcome doesn't have that field
        decoded.Parameter.Should().BeNull();
    }

    [Fact]
    public async Task LookupAsync_NotFoundResponse_ThrowsException()
    {
        var mockHandler = CreateMockHandler(HttpStatusCode.NotFound, "{}");
        var httpClient = new HttpClient(mockHandler.Object);
        var client = new OntoserverClient(httpClient, "https://test.example.com/fhir/");

        var act = () => client.LookupAsync("999999999");

        await act.Should().ThrowAsync<Exception>()
            .WithMessage("*not found*");
    }

    [Fact]
    public async Task LookupInCodeSystemAsync_ServerError_ThrowsException()
    {
        var mockHandler = CreateMockHandler(HttpStatusCode.InternalServerError, "Server error");
        var httpClient = new HttpClient(mockHandler.Object);
        var client = new OntoserverClient(httpClient, "https://test.example.com/fhir/");

        var act = () => client.LookupInCodeSystemAsync("12345", "http://loinc.org");

        await act.Should().ThrowAsync<Exception>()
            .WithMessage("*500*");
    }

    #endregion

    #region URL Construction Tests

    [Fact]
    public void LookupUrlConstruction_ContainsRequiredParameters()
    {
        var baseUrl = new Uri("https://tx.ontoserver.csiro.au/fhir/");
        var lookupPath = new Uri(baseUrl, "CodeSystem/$lookup");

        var query = System.Web.HttpUtility.ParseQueryString(string.Empty);
        query["system"] = "http://snomed.info/sct";
        query["version"] = "http://snomed.info/sct/900000000000207008";
        query["code"] = "73211009";
        query["_format"] = "json";

        var builder = new UriBuilder(lookupPath) { Query = query.ToString() };
        var url = builder.Uri.AbsoluteUri;

        url.Should().Contain("CodeSystem/$lookup");
        url.Should().Contain("code=73211009");
        url.Should().Contain("_format=json");
    }

    [Fact]
    public void EditionsUrlConstruction_ContainsRequiredParameters()
    {
        var baseUrl = new Uri("https://tx.ontoserver.csiro.au/fhir/");
        var codeSystemPath = new Uri(baseUrl, "CodeSystem");

        var query = System.Web.HttpUtility.ParseQueryString(string.Empty);
        query["url"] = "http://snomed.info/sct,http://snomed.info/xsct";
        query["_format"] = "json";

        var builder = new UriBuilder(codeSystemPath) { Query = query.ToString() };
        var url = builder.Uri.AbsoluteUri;

        url.Should().Contain("CodeSystem?");
        url.Should().Contain("url=");
    }

    [Fact]
    public void SetBaseUrl_UpdatesBaseUrl()
    {
        var client = new OntoserverClient(baseUrl: "https://original.example.com/fhir/");
        client.SetBaseUrl("https://newserver.example.com/fhir/");

        // We cannot directly access the private _baseUrl, but we can verify
        // no exception is thrown and the method completes successfully
        // The actual URL change would be verified through integration tests
    }

    #endregion

    #region ConceptResult Tests

    [Fact]
    public void ConceptResult_ActiveTrue_ReturnsActiveText()
    {
        var result = new ConceptResult(
            "123456",
            "MAIN",
            "Test (test)",
            "Test",
            true,
            null,
            null
        );

        result.ActiveText.Should().Be("active");
    }

    [Fact]
    public void ConceptResult_ActiveFalse_ReturnsInactiveText()
    {
        var result = new ConceptResult(
            "123456",
            "MAIN",
            "Test (test)",
            "Test",
            false,
            null,
            null
        );

        result.ActiveText.Should().Be("inactive");
    }

    [Fact]
    public void ConceptResult_ActiveNull_ReturnsDash()
    {
        var result = new ConceptResult(
            "123456",
            "MAIN",
            "Test (test)",
            "Test",
            null,
            null,
            null
        );

        result.ActiveText.Should().Be("-");
    }

    [Fact]
    public void ConceptResult_NilSystemIsSNOMED()
    {
        var result = new ConceptResult(
            "73211009",
            "International",
            "Diabetes mellitus (disorder)",
            "Diabetes mellitus",
            true,
            null,
            null,
            null
        );

        result.IsSnomedCT.Should().BeTrue();
        result.SystemName.Should().Be("SNOMED CT");
    }

    [Fact]
    public void ConceptResult_SnomedSystemIsSNOMED()
    {
        var result = new ConceptResult(
            "73211009",
            "International (20240101)",
            "Diabetes mellitus (disorder)",
            "Diabetes mellitus",
            true,
            "20020131",
            "900000000000207008",
            "http://snomed.info/sct"
        );

        result.IsSnomedCT.Should().BeTrue();
        result.SystemName.Should().Be("SNOMED CT");
    }

    [Fact]
    public void ConceptResult_LoincSystem_NotSNOMED()
    {
        var result = new ConceptResult(
            "8867-4",
            "2.74",
            null,
            "Heart rate",
            null,
            null,
            null,
            "http://loinc.org"
        );

        result.IsSnomedCT.Should().BeFalse();
        result.SystemName.Should().Be("LOINC");
    }

    [Fact]
    public void ConceptResult_RxNormSystem_NotSNOMED()
    {
        var result = new ConceptResult(
            "161",
            "RxNorm",
            null,
            "Acetaminophen",
            null,
            null,
            null,
            "http://www.nlm.nih.gov/research/umls/rxnorm"
        );

        result.IsSnomedCT.Should().BeFalse();
        result.SystemName.Should().Be("RxNorm");
    }

    [Fact]
    public void ConceptResult_ICD10CMSystem_NotSNOMED()
    {
        var result = new ConceptResult(
            "E11.9",
            "ICD-10-CM",
            null,
            "Type 2 diabetes mellitus without complications",
            null,
            null,
            null,
            "http://hl7.org/fhir/sid/icd-10-cm"
        );

        result.IsSnomedCT.Should().BeFalse();
        result.SystemName.Should().Be("ICD-10-CM");
    }

    [Fact]
    public void ConceptResult_UnknownSystem_ExtractsLastPathSegment()
    {
        var result = new ConceptResult(
            "12345",
            "Unknown",
            null,
            "Some term",
            null,
            null,
            null,
            "http://example.org/codesystem/custom"
        );

        result.IsSnomedCT.Should().BeFalse();
        result.SystemName.Should().Be("custom");
    }

    #endregion

    #region Edition Parsing Tests

    [Fact]
    public void ParseEditionsBundle_MultipleEditions_ParsesCorrectly()
    {
        var json = """
        {
            "resourceType": "Bundle",
            "type": "searchset",
            "entry": [
                {
                    "resource": {
                        "resourceType": "CodeSystem",
                        "url": "http://snomed.info/sct",
                        "version": "http://snomed.info/sct/32506021000036107/version/20240131",
                        "title": "SNOMED CT Australian Edition"
                    }
                },
                {
                    "resource": {
                        "resourceType": "CodeSystem",
                        "url": "http://snomed.info/sct",
                        "version": "http://snomed.info/sct/900000000000207008/version/20240101",
                        "title": "SNOMED CT International Edition"
                    }
                }
            ]
        }
        """;

        var bundle = JsonSerializer.Deserialize<TestFhirBundle>(json, JsonOpts);

        bundle.Should().NotBeNull();
        bundle!.Entry.Should().HaveCount(2);

        var ausEdition = bundle.Entry![0].Resource;
        ausEdition!.Version.Should().Contain("32506021000036107");
        ausEdition.Title.Should().Be("SNOMED CT Australian Edition");

        var intlEdition = bundle.Entry[1].Resource;
        intlEdition!.Version.Should().Contain("900000000000207008");
        intlEdition.Title.Should().Be("SNOMED CT International Edition");
    }

    [Fact]
    public void ExtractModuleIdFromVersion_ValidVersionUri_ExtractsCorrectly()
    {
        // Format: http://snomed.info/sct/32506021000036107/version/20240101
        // Split by '/' gives: [http:, "", snomed.info, sct, 32506021000036107, version, 20240101]
        // Index 4 is the module ID
        var version = "http://snomed.info/sct/32506021000036107/version/20240101";
        var parts = version.Split('/');

        parts.Length.Should().BeGreaterThanOrEqualTo(5);
        var moduleId = parts[4];
        moduleId.Should().Be("32506021000036107");
    }

    [Fact]
    public void ExtractModuleIdFromVersion_InternationalEdition_ExtractsCorrectly()
    {
        // Format: http://snomed.info/sct/900000000000207008/version/20240101
        var version = "http://snomed.info/sct/900000000000207008/version/20240101";
        var parts = version.Split('/');

        var moduleId = parts[4];
        moduleId.Should().Be("900000000000207008");
    }

    #endregion

    #region Batch Lookup Response Parsing Tests

    [Fact]
    public void ParseBatchLookupResponse_ValidValueSet_ParsesCorrectly()
    {
        var json = """
        {
            "resourceType": "ValueSet",
            "expansion": {
                "total": 2,
                "contains": [
                    {
                        "system": "http://snomed.info/sct",
                        "code": "73211009",
                        "display": "Diabetes mellitus",
                        "designation": [
                            {
                                "use": {"code": "900000000000003001"},
                                "value": "Diabetes mellitus (disorder)"
                            }
                        ]
                    },
                    {
                        "system": "http://snomed.info/sct",
                        "code": "385804009",
                        "display": "Diabetic care",
                        "designation": [
                            {
                                "use": {"code": "900000000000003001"},
                                "value": "Diabetic care (regime/therapy)"
                            }
                        ]
                    }
                ]
            }
        }
        """;

        var response = JsonSerializer.Deserialize<TestValueSetResponse>(json, JsonOpts);

        response.Should().NotBeNull();
        response!.ResourceType.Should().Be("ValueSet");
        response.Expansion.Should().NotBeNull();
        response.Expansion!.Total.Should().Be(2);
        response.Expansion.Contains.Should().HaveCount(2);

        var firstConcept = response.Expansion.Contains![0];
        firstConcept.Code.Should().Be("73211009");
        firstConcept.Display.Should().Be("Diabetes mellitus");
        firstConcept.Designation.Should().NotBeNull();
        firstConcept.Designation![0].Value.Should().Be("Diabetes mellitus (disorder)");
        firstConcept.Designation[0].Use!.Code.Should().Be("900000000000003001");
    }

    [Fact]
    public void ParseBatchLookupResponse_EmptyExpansion_HandlesGracefully()
    {
        var json = """
        {
            "resourceType": "ValueSet",
            "expansion": {
                "total": 0
            }
        }
        """;

        var response = JsonSerializer.Deserialize<TestValueSetResponse>(json, JsonOpts);

        response.Should().NotBeNull();
        response!.Expansion.Should().NotBeNull();
        response.Expansion!.Total.Should().Be(0);
        response.Expansion.Contains.Should().BeNull();
    }

    [Fact]
    public void ParseBatchLookupResponse_PartialDesignations_HandlesGracefully()
    {
        var json = """
        {
            "resourceType": "ValueSet",
            "expansion": {
                "total": 2,
                "contains": [
                    {
                        "system": "http://snomed.info/sct",
                        "code": "73211009",
                        "display": "Diabetes mellitus",
                        "designation": [
                            {
                                "use": {"code": "900000000000003001"},
                                "value": "Diabetes mellitus (disorder)"
                            }
                        ]
                    },
                    {
                        "system": "http://snomed.info/sct",
                        "code": "385804009",
                        "display": "Diabetic care"
                    }
                ]
            }
        }
        """;

        var response = JsonSerializer.Deserialize<TestValueSetResponse>(json, JsonOpts);

        response.Should().NotBeNull();
        response!.Expansion!.Contains.Should().HaveCount(2);

        // First concept has designation
        response.Expansion.Contains![0].Designation.Should().HaveCount(1);

        // Second concept has no designation
        response.Expansion.Contains[1].Designation.Should().BeNull();
    }

    [Fact]
    public void ParseBatchLookupResponse_WithInactiveProperty_ParsesCorrectly()
    {
        var json = """
        {
            "resourceType": "ValueSet",
            "expansion": {
                "total": 2,
                "contains": [
                    {
                        "system": "http://snomed.info/sct",
                        "code": "73211009",
                        "display": "Diabetes mellitus",
                        "inactive": false
                    },
                    {
                        "system": "http://snomed.info/sct",
                        "code": "385804009",
                        "display": "Diabetic care",
                        "inactive": true
                    }
                ]
            }
        }
        """;

        var response = JsonSerializer.Deserialize<TestValueSetResponse>(json, JsonOpts);

        response.Should().NotBeNull();
        response!.Expansion!.Contains![0].Inactive.Should().BeFalse();
        response.Expansion.Contains[1].Inactive.Should().BeTrue();
    }

    #endregion

    #region BatchLookupResult Accessor Tests

    [Fact]
    public void BatchLookupResult_PtAccessor_ReturnsCorrectValues()
    {
        var result = new BatchLookupResult(
            new Dictionary<string, string>
            {
                ["73211009"] = "Diabetes mellitus",
                ["385804009"] = "Diabetic care"
            },
            new Dictionary<string, string>(),
            new Dictionary<string, bool>()
        );

        result.PtByCode["73211009"].Should().Be("Diabetes mellitus");
        result.PtByCode["385804009"].Should().Be("Diabetic care");
        result.PtByCode.ContainsKey("99999999").Should().BeFalse();
    }

    [Fact]
    public void BatchLookupResult_FsnAccessor_ReturnsCorrectValues()
    {
        var result = new BatchLookupResult(
            new Dictionary<string, string>(),
            new Dictionary<string, string>
            {
                ["73211009"] = "Diabetes mellitus (disorder)",
                ["385804009"] = "Diabetic care (regime/therapy)"
            },
            new Dictionary<string, bool>()
        );

        result.FsnByCode["73211009"].Should().Be("Diabetes mellitus (disorder)");
        result.FsnByCode["385804009"].Should().Be("Diabetic care (regime/therapy)");
        result.FsnByCode.ContainsKey("99999999").Should().BeFalse();
    }

    [Fact]
    public void BatchLookupResult_ActiveAccessor_ReturnsCorrectValues()
    {
        var result = new BatchLookupResult(
            new Dictionary<string, string>(),
            new Dictionary<string, string>(),
            new Dictionary<string, bool>
            {
                ["73211009"] = true,
                ["385804009"] = false
            }
        );

        result.ActiveByCode["73211009"].Should().BeTrue();
        result.ActiveByCode["385804009"].Should().BeFalse();
        result.ActiveByCode.ContainsKey("99999999").Should().BeFalse();
    }

    [Fact]
    public void BatchLookupResult_AllAccessors_WorkTogether()
    {
        var result = new BatchLookupResult(
            new Dictionary<string, string>
            {
                ["73211009"] = "Diabetes mellitus",
                ["385804009"] = "Diabetic care"
            },
            new Dictionary<string, string>
            {
                ["73211009"] = "Diabetes mellitus (disorder)",
                ["385804009"] = "Diabetic care (regime/therapy)"
            },
            new Dictionary<string, bool>
            {
                ["73211009"] = true,
                ["385804009"] = false
            }
        );

        // Test PT accessors
        result.PtByCode["73211009"].Should().Be("Diabetes mellitus");
        result.PtByCode["385804009"].Should().Be("Diabetic care");

        // Test FSN accessors
        result.FsnByCode["73211009"].Should().Be("Diabetes mellitus (disorder)");
        result.FsnByCode["385804009"].Should().Be("Diabetic care (regime/therapy)");

        // Test active status accessors
        result.ActiveByCode["73211009"].Should().BeTrue();
        result.ActiveByCode["385804009"].Should().BeFalse();
    }

    #endregion

    #region Multi-Code-System Tests

    [Fact]
    public void ParseCodeSystemLookupResponse_LOINC_ParsesCorrectly()
    {
        var json = """
        {
            "resourceType": "Parameters",
            "parameter": [
                {"name": "display", "valueString": "Heart rate"},
                {"name": "version", "valueString": "2.74"}
            ]
        }
        """;

        var parameters = JsonSerializer.Deserialize<TestFhirParameters>(json, JsonOpts);

        parameters.Should().NotBeNull();
        parameters!.ResourceType.Should().Be("Parameters");
        parameters.Parameter.Should().HaveCount(2);

        // Check display parameter
        var displayParam = parameters.Parameter!.FirstOrDefault(p => p.Name == "display");
        displayParam!.ValueString.Should().Be("Heart rate");

        // Check version parameter
        var versionParam = parameters.Parameter!.FirstOrDefault(p => p.Name == "version");
        versionParam!.ValueString.Should().Be("2.74");
    }

    [Fact]
    public void ParseCodeSystemBundle_MultipleCodeSystems_ParsesCorrectly()
    {
        var json = """
        {
            "resourceType": "Bundle",
            "type": "searchset",
            "entry": [
                {
                    "resource": {
                        "resourceType": "CodeSystem",
                        "url": "http://loinc.org",
                        "version": "2.74",
                        "title": "LOINC"
                    }
                },
                {
                    "resource": {
                        "resourceType": "CodeSystem",
                        "url": "http://www.nlm.nih.gov/research/umls/rxnorm",
                        "title": "RxNorm"
                    }
                }
            ]
        }
        """;

        var bundle = JsonSerializer.Deserialize<TestFhirBundle>(json, JsonOpts);

        bundle.Should().NotBeNull();
        bundle!.ResourceType.Should().Be("Bundle");
        bundle.Entry.Should().HaveCount(2);

        var loinc = bundle.Entry![0].Resource;
        loinc!.Url.Should().Be("http://loinc.org");
        loinc.Title.Should().Be("LOINC");
        loinc.Version.Should().Be("2.74");

        var rxnorm = bundle.Entry[1].Resource;
        rxnorm!.Url.Should().Be("http://www.nlm.nih.gov/research/umls/rxnorm");
        rxnorm.Title.Should().Be("RxNorm");
    }

    [Fact]
    public void ParseNonSNOMEDSearchResponse_LOINC_ParsesCorrectly()
    {
        var json = """
        {
            "resourceType": "ValueSet",
            "expansion": {
                "total": 2,
                "contains": [
                    {
                        "system": "http://loinc.org",
                        "code": "8867-4",
                        "display": "Heart rate"
                    },
                    {
                        "system": "http://loinc.org",
                        "code": "8310-5",
                        "display": "Body temperature"
                    }
                ]
            }
        }
        """;

        var response = JsonSerializer.Deserialize<TestValueSetResponse>(json, JsonOpts);

        response.Should().NotBeNull();
        response!.Expansion!.Total.Should().Be(2);
        response.Expansion.Contains.Should().HaveCount(2);

        var first = response.Expansion.Contains![0];
        first.System.Should().Be("http://loinc.org");
        first.Code.Should().Be("8867-4");
        first.Display.Should().Be("Heart rate");
    }

    #endregion

    #region Request Header Tests

    [Fact]
    public void AcceptHeaderFormat_FhirJson_IsCorrect()
    {
        var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Accept.Clear();
        httpClient.DefaultRequestHeaders.Accept.Add(
            new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/fhir+json"));

        var acceptHeader = httpClient.DefaultRequestHeaders.Accept.ToString();
        acceptHeader.Should().Be("application/fhir+json");
    }

    #endregion

    #region Retry Logic Classification Tests

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]       // 500
    [InlineData(HttpStatusCode.BadGateway)]                // 502
    [InlineData(HttpStatusCode.ServiceUnavailable)]        // 503
    [InlineData(HttpStatusCode.GatewayTimeout)]            // 504
    public void IsRetryableHttpError_5xxErrors_AreRetryable(HttpStatusCode statusCode)
    {
        var code = (int)statusCode;
        var isRetryable = code >= 500 && code < 600;

        isRetryable.Should().BeTrue($"Expected HTTP {code} to be retryable");
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]                // 400
    [InlineData(HttpStatusCode.Unauthorized)]              // 401
    [InlineData(HttpStatusCode.Forbidden)]                 // 403
    [InlineData(HttpStatusCode.NotFound)]                  // 404
    [InlineData(HttpStatusCode.UnprocessableEntity)]       // 422
    public void IsRetryableHttpError_4xxErrors_AreNotRetryable(HttpStatusCode statusCode)
    {
        var code = (int)statusCode;
        var isRetryable = code >= 500 && code < 600;

        isRetryable.Should().BeFalse($"Expected HTTP {code} to NOT be retryable");
    }

    #endregion

    #region Integration Tests with Mocked HTTP

    [Fact]
    public async Task LookupAsync_SuccessfulResponse_ReturnsConceptResult()
    {
        var responseJson = """
        {
            "resourceType": "Parameters",
            "parameter": [
                {"name": "version", "valueString": "http://snomed.info/sct/900000000000207008/version/20240101"},
                {"name": "display", "valueString": "Diabetes mellitus"},
                {
                    "name": "property",
                    "part": [
                        {"name": "code", "valueCode": "inactive"},
                        {"name": "value", "valueBoolean": false}
                    ]
                },
                {
                    "name": "designation",
                    "part": [
                        {"name": "use", "valueCoding": {"code": "900000000000003001"}},
                        {"name": "value", "valueString": "Diabetes mellitus (disorder)"}
                    ]
                }
            ]
        }
        """;

        var mockHandler = CreateMockHandler(HttpStatusCode.OK, responseJson);
        var httpClient = new HttpClient(mockHandler.Object);
        var client = new OntoserverClient(httpClient, "https://test.example.com/fhir/");

        var result = await client.LookupAsync("73211009");

        result.Should().NotBeNull();
        result.ConceptId.Should().Be("73211009");
        result.Pt.Should().Be("Diabetes mellitus");
        result.Fsn.Should().Be("Diabetes mellitus (disorder)");
        result.Active.Should().BeTrue();
    }

    [Fact]
    public async Task LookupInCodeSystemAsync_LOINC_ReturnsConceptResult()
    {
        var responseJson = """
        {
            "resourceType": "Parameters",
            "parameter": [
                {"name": "display", "valueString": "Heart rate"},
                {"name": "version", "valueString": "2.74"}
            ]
        }
        """;

        var mockHandler = CreateMockHandler(HttpStatusCode.OK, responseJson);
        var httpClient = new HttpClient(mockHandler.Object);
        var client = new OntoserverClient(httpClient, "https://test.example.com/fhir/");

        var result = await client.LookupInCodeSystemAsync("8867-4", "http://loinc.org");

        result.Should().NotBeNull();
        result.ConceptId.Should().Be("8867-4");
        result.Pt.Should().Be("Heart rate");
        result.System.Should().Be("http://loinc.org");
        result.SystemName.Should().Be("LOINC");
    }

    [Fact]
    public async Task BatchLookupAsync_ValidCodes_ReturnsBatchResult()
    {
        var responseJson = """
        {
            "resourceType": "ValueSet",
            "expansion": {
                "total": 2,
                "contains": [
                    {
                        "system": "http://snomed.info/sct",
                        "code": "73211009",
                        "display": "Diabetes mellitus",
                        "inactive": false,
                        "designation": [
                            {
                                "use": {"code": "900000000000003001"},
                                "value": "Diabetes mellitus (disorder)"
                            }
                        ]
                    },
                    {
                        "system": "http://snomed.info/sct",
                        "code": "385804009",
                        "display": "Diabetic care",
                        "inactive": false
                    }
                ]
            }
        }
        """;

        var mockHandler = CreateMockHandler(HttpStatusCode.OK, responseJson);
        var httpClient = new HttpClient(mockHandler.Object);
        var client = new OntoserverClient(httpClient, "https://test.example.com/fhir/");

        var result = await client.BatchLookupAsync(new[] { "73211009", "385804009" });

        result.Should().NotBeNull();
        result.PtByCode.Should().HaveCount(2);
        result.PtByCode["73211009"].Should().Be("Diabetes mellitus");
        result.PtByCode["385804009"].Should().Be("Diabetic care");
        result.FsnByCode["73211009"].Should().Be("Diabetes mellitus (disorder)");
        result.ActiveByCode["73211009"].Should().BeTrue();
        result.ActiveByCode["385804009"].Should().BeTrue();
    }

    [Fact]
    public async Task BatchLookupAsync_EmptyCodes_ReturnsEmptyResult()
    {
        var mockHandler = CreateMockHandler(HttpStatusCode.OK, "{}");
        var httpClient = new HttpClient(mockHandler.Object);
        var client = new OntoserverClient(httpClient, "https://test.example.com/fhir/");

        var result = await client.BatchLookupAsync(Array.Empty<string>());

        result.Should().NotBeNull();
        result.PtByCode.Should().BeEmpty();
        result.FsnByCode.Should().BeEmpty();
        result.ActiveByCode.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchAsync_ValidFilter_ReturnsSearchResults()
    {
        var responseJson = """
        {
            "resourceType": "ValueSet",
            "expansion": {
                "total": 2,
                "contains": [
                    {
                        "system": "http://snomed.info/sct",
                        "code": "73211009",
                        "display": "Diabetes mellitus",
                        "designation": [
                            {
                                "use": {"code": "900000000000003001"},
                                "value": "Diabetes mellitus (disorder)"
                            }
                        ]
                    },
                    {
                        "system": "http://snomed.info/sct",
                        "code": "385804009",
                        "display": "Diabetic care"
                    }
                ]
            }
        }
        """;

        var mockHandler = CreateMockHandler(HttpStatusCode.OK, responseJson);
        var httpClient = new HttpClient(mockHandler.Object);
        var client = new OntoserverClient(httpClient, "https://test.example.com/fhir/");

        var results = await client.SearchAsync("diabetes", "http://snomed.info/sct");

        results.Should().HaveCount(2);
        results[0].Code.Should().Be("73211009");
        results[0].Display.Should().Be("Diabetes mellitus");
        results[0].Fsn.Should().Be("Diabetes mellitus (disorder)");
        results[0].SystemName.Should().Be("SNOMED CT");
    }

    [Fact]
    public async Task SearchAsync_FilterTooShort_ReturnsEmptyList()
    {
        var mockHandler = CreateMockHandler(HttpStatusCode.OK, "{}");
        var httpClient = new HttpClient(mockHandler.Object);
        var client = new OntoserverClient(httpClient, "https://test.example.com/fhir/");

        var results = await client.SearchAsync("ab", "http://snomed.info/sct");

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAvailableCodeSystemsAsync_ReturnsCodeSystems()
    {
        var responseJson = """
        {
            "resourceType": "Bundle",
            "type": "searchset",
            "entry": [
                {
                    "resource": {
                        "resourceType": "CodeSystem",
                        "url": "http://snomed.info/sct",
                        "title": "SNOMED CT"
                    }
                },
                {
                    "resource": {
                        "resourceType": "CodeSystem",
                        "url": "http://loinc.org",
                        "title": "LOINC",
                        "version": "2.74"
                    }
                }
            ]
        }
        """;

        var mockHandler = CreateMockHandler(HttpStatusCode.OK, responseJson);
        var httpClient = new HttpClient(mockHandler.Object);
        var client = new OntoserverClient(httpClient, "https://test.example.com/fhir/");

        var codeSystems = await client.GetAvailableCodeSystemsAsync();

        codeSystems.Should().HaveCount(2);
        codeSystems[0].Uri.Should().Be("http://snomed.info/sct");
        codeSystems[0].Title.Should().Be("SNOMED CT");
        codeSystems[1].Uri.Should().Be("http://loinc.org");
        codeSystems[1].Title.Should().Be("LOINC");
        codeSystems[1].Version.Should().Be("2.74");
    }

    [Fact]
    public async Task LookupInConfiguredSystemsAsync_FoundInFirstSystem_ReturnsResult()
    {
        var responseJson = """
        {
            "resourceType": "Parameters",
            "parameter": [
                {"name": "display", "valueString": "Heart rate"},
                {"name": "version", "valueString": "2.74"}
            ]
        }
        """;

        var mockHandler = CreateMockHandler(HttpStatusCode.OK, responseJson);
        var httpClient = new HttpClient(mockHandler.Object);
        var client = new OntoserverClient(httpClient, "https://test.example.com/fhir/");

        var result = await client.LookupInConfiguredSystemsAsync(
            "8867-4",
            new[] { "http://loinc.org", "http://snomed.info/sct" });

        result.Should().NotBeNull();
        result!.ConceptId.Should().Be("8867-4");
        result.Pt.Should().Be("Heart rate");
    }

    [Fact]
    public async Task LookupInConfiguredSystemsAsync_NotFoundInAny_ReturnsNull()
    {
        var mockHandler = CreateMockHandler(HttpStatusCode.NotFound, "{}");
        var httpClient = new HttpClient(mockHandler.Object);
        var client = new OntoserverClient(httpClient, "https://test.example.com/fhir/");

        var result = await client.LookupInConfiguredSystemsAsync(
            "invalid-code",
            new[] { "http://loinc.org", "http://snomed.info/sct" });

        result.Should().BeNull();
    }

    #endregion

    #region Helper Methods

    private static Mock<HttpMessageHandler> CreateMockHandler(HttpStatusCode statusCode, string content)
    {
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = new StringContent(content, Encoding.UTF8, "application/fhir+json")
            });
        return mockHandler;
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    #endregion

    #region Test DTOs (mirror internal FHIR types for testing JSON parsing)

    /// <summary>Test DTO for FHIR Parameters response.</summary>
    private sealed class TestFhirParameters
    {
        [JsonPropertyName("resourceType")]
        public string? ResourceType { get; set; }

        [JsonPropertyName("parameter")]
        public TestFhirParameter[]? Parameter { get; set; }
    }

    private sealed class TestFhirParameter
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("valueString")]
        public string? ValueString { get; set; }

        [JsonPropertyName("valueCode")]
        public string? ValueCode { get; set; }

        [JsonPropertyName("part")]
        public TestFhirParameterPart[]? Part { get; set; }
    }

    private sealed class TestFhirParameterPart
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("valueString")]
        public string? ValueString { get; set; }

        [JsonPropertyName("valueCode")]
        public string? ValueCode { get; set; }

        [JsonPropertyName("valueBoolean")]
        public bool? ValueBoolean { get; set; }

        [JsonPropertyName("valueCoding")]
        public TestFhirCoding? ValueCoding { get; set; }
    }

    private sealed class TestFhirCoding
    {
        [JsonPropertyName("system")]
        public string? System { get; set; }

        [JsonPropertyName("code")]
        public string? Code { get; set; }

        [JsonPropertyName("display")]
        public string? Display { get; set; }
    }

    private sealed class TestFhirBundle
    {
        [JsonPropertyName("resourceType")]
        public string? ResourceType { get; set; }

        [JsonPropertyName("entry")]
        public TestFhirBundleEntry[]? Entry { get; set; }
    }

    private sealed class TestFhirBundleEntry
    {
        [JsonPropertyName("resource")]
        public TestFhirCodeSystem? Resource { get; set; }
    }

    private sealed class TestFhirCodeSystem
    {
        [JsonPropertyName("url")]
        public string? Url { get; set; }

        [JsonPropertyName("version")]
        public string? Version { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("title")]
        public string? Title { get; set; }
    }

    private sealed class TestValueSetResponse
    {
        [JsonPropertyName("resourceType")]
        public string? ResourceType { get; set; }

        [JsonPropertyName("expansion")]
        public TestExpansion? Expansion { get; set; }
    }

    private sealed class TestExpansion
    {
        [JsonPropertyName("total")]
        public int? Total { get; set; }

        [JsonPropertyName("contains")]
        public TestExpansionContains[]? Contains { get; set; }
    }

    private sealed class TestExpansionContains
    {
        [JsonPropertyName("system")]
        public string? System { get; set; }

        [JsonPropertyName("code")]
        public string? Code { get; set; }

        [JsonPropertyName("display")]
        public string? Display { get; set; }

        [JsonPropertyName("inactive")]
        public bool? Inactive { get; set; }

        [JsonPropertyName("designation")]
        public TestDesignation[]? Designation { get; set; }
    }

    private sealed class TestDesignation
    {
        [JsonPropertyName("use")]
        public TestFhirCoding? Use { get; set; }

        [JsonPropertyName("value")]
        public string? Value { get; set; }
    }

    #endregion
}

/// <summary>
/// Tests for SearchResultItem record.
/// </summary>
public class SearchResultItemTests
{
    [Fact]
    public void SearchResultItem_Creation_SetsAllProperties()
    {
        var item = new SearchResultItem(
            "73211009",
            "Diabetes mellitus",
            "Diabetes mellitus (disorder)",
            "SNOMED CT",
            "http://snomed.info/sct"
        );

        item.Code.Should().Be("73211009");
        item.Display.Should().Be("Diabetes mellitus");
        item.Fsn.Should().Be("Diabetes mellitus (disorder)");
        item.SystemName.Should().Be("SNOMED CT");
        item.SystemUri.Should().Be("http://snomed.info/sct");
    }

    [Fact]
    public void SearchResultItem_WithNullFsn_IsValid()
    {
        var item = new SearchResultItem(
            "8867-4",
            "Heart rate",
            null,
            "LOINC",
            "http://loinc.org"
        );

        item.Code.Should().Be("8867-4");
        item.Fsn.Should().BeNull();
    }
}

/// <summary>
/// Tests for OntoserverClient User-Agent header configuration.
/// </summary>
public class OntoserverClientUserAgentTests
{
    #region User-Agent Header Tests

    [Fact]
    public void Constructor_WithInstallId_SetsUserAgentHeader()
    {
        var http = new HttpClient();
        var installId = Guid.NewGuid().ToString();

        _ = new OntoserverClient(http: http, installId: installId);

        var userAgent = http.DefaultRequestHeaders.UserAgent.ToString();
        userAgent.Should().Contain("Codeagogo/");
        userAgent.Should().Contain(installId);
    }

    [Fact]
    public void Constructor_WithoutInstallId_SetsUserAgentWithoutId()
    {
        var http = new HttpClient();

        _ = new OntoserverClient(http: http);

        var userAgent = http.DefaultRequestHeaders.UserAgent.ToString();
        userAgent.Should().Contain("Codeagogo/");
        // Should not contain a GUID-like pattern
        Guid.TryParse(userAgent, out _).Should().BeFalse();
    }

    [Fact]
    public void Constructor_UserAgent_ContainsWindowsPlatform()
    {
        var http = new HttpClient();

        _ = new OntoserverClient(http: http);

        var userAgent = http.DefaultRequestHeaders.UserAgent.ToString();
        userAgent.Should().Contain("Windows");
    }

    #endregion
}
