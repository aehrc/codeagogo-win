// Copyright 2026 CSIRO. Licensed under the Apache License, Version 2.0.
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Net.Http;
using System.Text.Json;
using FluentAssertions;
using Moq;
using Moq.Protected;

namespace Codeagogo.Tests;

/// <summary>
/// Unit tests for SearchViewModel search functionality, formatting, and state management.
/// Translated from the Mac version (SearchTests.swift and SearchViewModelTests.swift)
/// with adaptations for the Windows architecture.
/// </summary>
public class SearchViewModelTests
{
    #region InsertFormat Tests

    [Fact]
    public void InsertFormat_IdOnly_HasCorrectValue()
    {
        InsertFormat.IdOnly.Should().Be(InsertFormat.IdOnly);
    }

    [Fact]
    public void InsertFormat_PtOnly_HasCorrectValue()
    {
        InsertFormat.PtOnly.Should().Be(InsertFormat.PtOnly);
    }

    [Fact]
    public void InsertFormat_FsnOnly_HasCorrectValue()
    {
        InsertFormat.FsnOnly.Should().Be(InsertFormat.FsnOnly);
    }

    [Fact]
    public void InsertFormat_IdPipePT_HasCorrectValue()
    {
        InsertFormat.IdPipePT.Should().Be(InsertFormat.IdPipePT);
    }

    [Fact]
    public void InsertFormat_IdPipeFSN_HasCorrectValue()
    {
        InsertFormat.IdPipeFSN.Should().Be(InsertFormat.IdPipeFSN);
    }

    [Fact]
    public void InsertFormat_EnumHasFiveValues()
    {
        Enum.GetValues<InsertFormat>().Should().HaveCount(5);
    }

    #endregion

    #region SearchResultItem Tests

    [Fact]
    public void SearchResultItem_StoresAllProperties()
    {
        var result = CreateTestSearchResult();

        result.Code.Should().Be("387517004");
        result.Display.Should().Be("Paracetamol");
        result.Fsn.Should().Be("Paracetamol (product)");
        result.SystemName.Should().Be("SNOMED CT");
        result.SystemUri.Should().Be("http://snomed.info/sct");
    }

    [Fact]
    public void SearchResultItem_RecordEquality_SameValues_AreEqual()
    {
        var result1 = CreateTestSearchResult();
        var result2 = CreateTestSearchResult();

        result1.Should().Be(result2);
    }

    [Fact]
    public void SearchResultItem_RecordEquality_DifferentCodes_AreNotEqual()
    {
        var result1 = CreateTestSearchResult();
        var result2 = new SearchResultItem(
            "123456789",
            "Different Concept",
            "Different Concept (finding)",
            "SNOMED CT",
            "http://snomed.info/sct"
        );

        result1.Should().NotBe(result2);
    }

    [Fact]
    public void SearchResultItem_WithNullFsn_StoresNullCorrectly()
    {
        var result = new SearchResultItem(
            "387517004",
            "Paracetamol",
            null,
            "SNOMED CT",
            "http://snomed.info/sct"
        );

        result.Fsn.Should().BeNull();
    }

    [Fact]
    public void SearchResultItem_WithNullDisplay_StoresNullCorrectly()
    {
        var result = new SearchResultItem(
            "387517004",
            null,
            "Paracetamol (product)",
            "SNOMED CT",
            "http://snomed.info/sct"
        );

        result.Display.Should().BeNull();
    }

    #endregion

    #region FormatForInsertion Tests - IdOnly

    [Fact]
    public void FormatForInsertion_IdOnly_ReturnsCode()
    {
        var (viewModel, _) = CreateViewModelWithMockClient();
        var item = CreateTestSearchResult();
        viewModel.SelectedFormat = InsertFormat.IdOnly;

        var formatted = viewModel.FormatForInsertion(item);

        formatted.Should().Be("387517004");
    }

    #endregion

    #region FormatForInsertion Tests - PtOnly

    [Fact]
    public void FormatForInsertion_PtOnly_ReturnsDisplay()
    {
        var (viewModel, _) = CreateViewModelWithMockClient();
        var item = CreateTestSearchResult();
        viewModel.SelectedFormat = InsertFormat.PtOnly;

        var formatted = viewModel.FormatForInsertion(item);

        formatted.Should().Be("Paracetamol");
    }

    [Fact]
    public void FormatForInsertion_PtOnly_FallsBackToCode_WhenDisplayIsNull()
    {
        var (viewModel, _) = CreateViewModelWithMockClient();
        var item = new SearchResultItem("387517004", null, "Paracetamol (product)", "SNOMED CT", "http://snomed.info/sct");
        viewModel.SelectedFormat = InsertFormat.PtOnly;

        var formatted = viewModel.FormatForInsertion(item);

        formatted.Should().Be("387517004");
    }

    #endregion

    #region FormatForInsertion Tests - FsnOnly

    [Fact]
    public void FormatForInsertion_FsnOnly_ReturnsFsn()
    {
        var (viewModel, _) = CreateViewModelWithMockClient();
        var item = CreateTestSearchResult();
        viewModel.SelectedFormat = InsertFormat.FsnOnly;

        var formatted = viewModel.FormatForInsertion(item);

        formatted.Should().Be("Paracetamol (product)");
    }

    [Fact]
    public void FormatForInsertion_FsnOnly_FallsBackToDisplay_WhenFsnIsNull()
    {
        var (viewModel, _) = CreateViewModelWithMockClient();
        var item = new SearchResultItem("387517004", "Paracetamol", null, "SNOMED CT", "http://snomed.info/sct");
        viewModel.SelectedFormat = InsertFormat.FsnOnly;

        var formatted = viewModel.FormatForInsertion(item);

        formatted.Should().Be("Paracetamol");
    }

    [Fact]
    public void FormatForInsertion_FsnOnly_FallsBackToCode_WhenBothNull()
    {
        var (viewModel, _) = CreateViewModelWithMockClient();
        var item = new SearchResultItem("387517004", null, null, "SNOMED CT", "http://snomed.info/sct");
        viewModel.SelectedFormat = InsertFormat.FsnOnly;

        var formatted = viewModel.FormatForInsertion(item);

        formatted.Should().Be("387517004");
    }

    #endregion

    #region FormatForInsertion Tests - IdPipePT

    [Fact]
    public void FormatForInsertion_IdPipePT_ReturnsCorrectFormat()
    {
        var (viewModel, _) = CreateViewModelWithMockClient();
        var item = CreateTestSearchResult();
        viewModel.SelectedFormat = InsertFormat.IdPipePT;

        var formatted = viewModel.FormatForInsertion(item);

        formatted.Should().Be("387517004 |Paracetamol|");
    }

    [Fact]
    public void FormatForInsertion_IdPipePT_FallsBackToCode_WhenDisplayIsNull()
    {
        var (viewModel, _) = CreateViewModelWithMockClient();
        var item = new SearchResultItem("387517004", null, "Paracetamol (product)", "SNOMED CT", "http://snomed.info/sct");
        viewModel.SelectedFormat = InsertFormat.IdPipePT;

        var formatted = viewModel.FormatForInsertion(item);

        formatted.Should().Be("387517004 |387517004|");
    }

    #endregion

    #region FormatForInsertion Tests - IdPipeFSN

    [Fact]
    public void FormatForInsertion_IdPipeFSN_ReturnsCorrectFormat()
    {
        var (viewModel, _) = CreateViewModelWithMockClient();
        var item = CreateTestSearchResult();
        viewModel.SelectedFormat = InsertFormat.IdPipeFSN;

        var formatted = viewModel.FormatForInsertion(item);

        formatted.Should().Be("387517004 |Paracetamol (product)|");
    }

    [Fact]
    public void FormatForInsertion_IdPipeFSN_FallsBackToDisplay_WhenFsnIsNull()
    {
        var (viewModel, _) = CreateViewModelWithMockClient();
        var item = new SearchResultItem("387517004", "Paracetamol", null, "SNOMED CT", "http://snomed.info/sct");
        viewModel.SelectedFormat = InsertFormat.IdPipeFSN;

        var formatted = viewModel.FormatForInsertion(item);

        formatted.Should().Be("387517004 |Paracetamol|");
    }

    [Fact]
    public void FormatForInsertion_IdPipeFSN_FallsBackToCode_WhenBothNull()
    {
        var (viewModel, _) = CreateViewModelWithMockClient();
        var item = new SearchResultItem("387517004", null, null, "SNOMED CT", "http://snomed.info/sct");
        viewModel.SelectedFormat = InsertFormat.IdPipeFSN;

        var formatted = viewModel.FormatForInsertion(item);

        formatted.Should().Be("387517004 |387517004|");
    }

    #endregion

    #region ViewModel Initial State Tests

    [Fact]
    public void SearchViewModel_InitialState_HasEmptyResults()
    {
        var (viewModel, _) = CreateViewModelWithMockClient();

        viewModel.Results.Should().BeEmpty();
    }

    [Fact]
    public void SearchViewModel_InitialState_IsNotSearching()
    {
        var (viewModel, _) = CreateViewModelWithMockClient();

        viewModel.IsSearching.Should().BeFalse();
    }

    [Fact]
    public void SearchViewModel_InitialState_HasDefaultCodeSystem()
    {
        var (viewModel, _) = CreateViewModelWithMockClient();

        viewModel.SelectedCodeSystem.Should().Be("http://snomed.info/sct");
    }

    [Fact]
    public void SearchViewModel_InitialState_HasNullEditionUri()
    {
        var (viewModel, _) = CreateViewModelWithMockClient();

        viewModel.SelectedEditionUri.Should().BeNull();
    }

    [Fact]
    public void SearchViewModel_InitialState_HasSpecifiedDefaultFormat()
    {
        var (viewModel, _) = CreateViewModelWithMockClient(InsertFormat.IdPipeFSN);

        viewModel.SelectedFormat.Should().Be(InsertFormat.IdPipeFSN);
    }

    #endregion

    #region SearchDebounced Tests

    [Fact]
    public void SearchDebounced_ShortQuery_ClearsResults()
    {
        var (viewModel, _) = CreateViewModelWithMockClient();
        // First add some results
        viewModel.Results.Add(CreateTestSearchResult());

        viewModel.SearchDebounced("ab"); // Less than 3 characters

        viewModel.Results.Should().BeEmpty();
    }

    [Fact]
    public void SearchDebounced_EmptyQuery_ClearsResults()
    {
        var (viewModel, _) = CreateViewModelWithMockClient();
        viewModel.Results.Add(CreateTestSearchResult());

        viewModel.SearchDebounced("");

        viewModel.Results.Should().BeEmpty();
    }

    [Fact]
    public void SearchDebounced_WhitespaceOnlyQuery_ClearsResults()
    {
        var (viewModel, _) = CreateViewModelWithMockClient();
        viewModel.Results.Add(CreateTestSearchResult());

        viewModel.SearchDebounced("   ");

        viewModel.Results.Should().BeEmpty();
    }

    [Fact]
    public void SearchDebounced_NullQuery_ClearsResults()
    {
        var (viewModel, _) = CreateViewModelWithMockClient();
        viewModel.Results.Add(CreateTestSearchResult());

        viewModel.SearchDebounced(null!);

        viewModel.Results.Should().BeEmpty();
    }

    #endregion

    #region SelectedCodeSystem Tests

    [Fact]
    public void SelectedCodeSystem_CanBeChanged()
    {
        var (viewModel, _) = CreateViewModelWithMockClient();

        viewModel.SelectedCodeSystem = "http://loinc.org";

        viewModel.SelectedCodeSystem.Should().Be("http://loinc.org");
    }

    [Fact]
    public void SelectedEditionUri_CanBeSet()
    {
        var (viewModel, _) = CreateViewModelWithMockClient();

        viewModel.SelectedEditionUri = "http://snomed.info/sct/32506021000036107";

        viewModel.SelectedEditionUri.Should().Be("http://snomed.info/sct/32506021000036107");
    }

    #endregion

    #region SelectedFormat Tests

    [Fact]
    public void SelectedFormat_CanBeChanged()
    {
        var (viewModel, _) = CreateViewModelWithMockClient();

        viewModel.SelectedFormat = InsertFormat.IdOnly;

        viewModel.SelectedFormat.Should().Be(InsertFormat.IdOnly);
    }

    [Theory]
    [InlineData(InsertFormat.IdOnly)]
    [InlineData(InsertFormat.PtOnly)]
    [InlineData(InsertFormat.FsnOnly)]
    [InlineData(InsertFormat.IdPipePT)]
    [InlineData(InsertFormat.IdPipeFSN)]
    public void SelectedFormat_AllValuesCanBeSet(InsertFormat format)
    {
        var (viewModel, _) = CreateViewModelWithMockClient();

        viewModel.SelectedFormat = format;

        viewModel.SelectedFormat.Should().Be(format);
    }

    #endregion

    #region Results Population Tests

    [Fact]
    public void Results_CanBeCleared()
    {
        var (viewModel, _) = CreateViewModelWithMockClient();
        viewModel.Results.Add(CreateTestSearchResult());
        viewModel.Results.Add(new SearchResultItem("73211009", "Diabetes mellitus", "Diabetes mellitus (disorder)", "SNOMED CT", "http://snomed.info/sct"));

        viewModel.Results.Clear();

        viewModel.Results.Should().BeEmpty();
    }

    [Fact]
    public void Results_ObservableCollection_CanAddItems()
    {
        var (viewModel, _) = CreateViewModelWithMockClient();
        var result = CreateTestSearchResult();

        viewModel.Results.Add(result);

        viewModel.Results.Should().ContainSingle();
        viewModel.Results[0].Should().Be(result);
    }

    [Fact]
    public void Results_ObservableCollection_CanAddMultipleItems()
    {
        var (viewModel, _) = CreateViewModelWithMockClient();
        var result1 = CreateTestSearchResult();
        var result2 = new SearchResultItem("73211009", "Diabetes mellitus", "Diabetes mellitus (disorder)", "SNOMED CT", "http://snomed.info/sct");

        viewModel.Results.Add(result1);
        viewModel.Results.Add(result2);

        viewModel.Results.Should().HaveCount(2);
        viewModel.Results[0].Code.Should().Be("387517004");
        viewModel.Results[1].Code.Should().Be("73211009");
    }

    #endregion

    #region Format All Cases Tests

    [Fact]
    public void FormatForInsertion_AllFormats_ProduceValidOutput()
    {
        var (viewModel, _) = CreateViewModelWithMockClient();
        var item = CreateTestSearchResult();

        foreach (var format in Enum.GetValues<InsertFormat>())
        {
            viewModel.SelectedFormat = format;
            var formatted = viewModel.FormatForInsertion(item);

            formatted.Should().NotBeNullOrEmpty($"Format {format} should produce non-empty output");
        }
    }

    [Theory]
    [InlineData(InsertFormat.IdOnly)]
    [InlineData(InsertFormat.IdPipePT)]
    [InlineData(InsertFormat.IdPipeFSN)]
    public void FormatForInsertion_IdFormats_ContainCode(InsertFormat format)
    {
        var (viewModel, _) = CreateViewModelWithMockClient();
        var item = CreateTestSearchResult();
        viewModel.SelectedFormat = format;

        var formatted = viewModel.FormatForInsertion(item);

        formatted.Should().Contain("387517004", $"Format {format} should contain the code");
    }

    [Theory]
    [InlineData(InsertFormat.PtOnly)]
    [InlineData(InsertFormat.FsnOnly)]
    public void FormatForInsertion_TermOnlyFormats_ContainTerm(InsertFormat format)
    {
        var (viewModel, _) = CreateViewModelWithMockClient();
        var item = CreateTestSearchResult();
        viewModel.SelectedFormat = format;

        var formatted = viewModel.FormatForInsertion(item);

        // These formats should contain term text, not code
        formatted.Should().Contain("Paracetamol", $"Format {format} should contain the term");
    }

    #endregion

    #region Dispose Tests

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        var (viewModel, _) = CreateViewModelWithMockClient();

        var act = () =>
        {
            viewModel.Dispose();
            viewModel.Dispose();
        };

        act.Should().NotThrow();
    }

    [Fact]
    public void Dispose_AfterSearch_DoesNotThrow()
    {
        var (viewModel, _) = CreateViewModelWithMockClient();
        viewModel.SearchDebounced("test");

        var act = () => viewModel.Dispose();

        act.Should().NotThrow();
    }

    #endregion

    #region Clinical Use Case Tests

    [Fact]
    public void FormatForInsertion_DiabetesMellitus_IdPipePT_CorrectFormat()
    {
        var (viewModel, _) = CreateViewModelWithMockClient();
        var item = new SearchResultItem(
            "73211009",
            "Diabetes mellitus",
            "Diabetes mellitus (disorder)",
            "SNOMED CT",
            "http://snomed.info/sct"
        );
        viewModel.SelectedFormat = InsertFormat.IdPipePT;

        var formatted = viewModel.FormatForInsertion(item);

        formatted.Should().Be("73211009 |Diabetes mellitus|");
    }

    [Fact]
    public void FormatForInsertion_DiabetesMellitus_IdPipeFSN_CorrectFormat()
    {
        var (viewModel, _) = CreateViewModelWithMockClient();
        var item = new SearchResultItem(
            "73211009",
            "Diabetes mellitus",
            "Diabetes mellitus (disorder)",
            "SNOMED CT",
            "http://snomed.info/sct"
        );
        viewModel.SelectedFormat = InsertFormat.IdPipeFSN;

        var formatted = viewModel.FormatForInsertion(item);

        formatted.Should().Be("73211009 |Diabetes mellitus (disorder)|");
    }

    [Fact]
    public void FormatForInsertion_InternationalModuleId_IdOnly_CorrectFormat()
    {
        var (viewModel, _) = CreateViewModelWithMockClient();
        var item = new SearchResultItem(
            "900000000000207008",
            "SNOMED CT core module",
            "SNOMED CT core module (core metadata concept)",
            "SNOMED CT",
            "http://snomed.info/sct"
        );
        viewModel.SelectedFormat = InsertFormat.IdOnly;

        var formatted = viewModel.FormatForInsertion(item);

        formatted.Should().Be("900000000000207008");
    }

    #endregion

    #region Helper Methods

    private static SearchResultItem CreateTestSearchResult() =>
        new(
            "387517004",
            "Paracetamol",
            "Paracetamol (product)",
            "SNOMED CT",
            "http://snomed.info/sct"
        );

    private static (SearchViewModel ViewModel, Mock<HttpMessageHandler> MockHandler) CreateViewModelWithMockClient(
        InsertFormat defaultFormat = InsertFormat.IdPipeFSN)
    {
        var mockHandler = new Mock<HttpMessageHandler>();

        // Setup default empty response
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(new
                {
                    resourceType = "ValueSet",
                    expansion = new
                    {
                        total = 0,
                        contains = Array.Empty<object>()
                    }
                }))
            });

        var httpClient = new HttpClient(mockHandler.Object);
        var client = new OntoserverClient(httpClient);
        var viewModel = new SearchViewModel(client, defaultFormat);

        return (viewModel, mockHandler);
    }

    #endregion
}

/// <summary>
/// Tests for OntoserverClient search functionality.
/// </summary>
public class OntoserverClientSearchTests
{
    #region SearchAsync Basic Tests

    [Fact]
    public async Task SearchAsync_EmptyFilter_ReturnsEmptyList()
    {
        var client = CreateClientWithMockResponse(new { resourceType = "ValueSet" });

        var results = await client.SearchAsync("", "http://snomed.info/sct");

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchAsync_ShortFilter_ReturnsEmptyList()
    {
        var client = CreateClientWithMockResponse(new { resourceType = "ValueSet" });

        var results = await client.SearchAsync("ab", "http://snomed.info/sct");

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchAsync_ValidFilter_CallsServer()
    {
        var (client, mockHandler) = CreateClientWithMockHandler();

        await client.SearchAsync("diabetes", "http://snomed.info/sct");

        mockHandler.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task SearchAsync_ParsesResults_Correctly()
    {
        var response = new
        {
            resourceType = "ValueSet",
            expansion = new
            {
                total = 2,
                contains = new object[]
                {
                    new
                    {
                        system = "http://snomed.info/sct",
                        code = "73211009",
                        display = "Diabetes mellitus",
                        designation = new[]
                        {
                            new
                            {
                                use = new { system = "http://snomed.info/sct", code = "900000000000003001" },
                                value = "Diabetes mellitus (disorder)"
                            }
                        }
                    },
                    new
                    {
                        system = "http://snomed.info/sct",
                        code = "387517004",
                        display = "Paracetamol"
                    }
                }
            }
        };

        var client = CreateClientWithMockResponse(response);

        var results = await client.SearchAsync("test", "http://snomed.info/sct");

        results.Should().HaveCount(2);
        results[0].Code.Should().Be("73211009");
        results[0].Display.Should().Be("Diabetes mellitus");
        results[0].Fsn.Should().Be("Diabetes mellitus (disorder)");
        results[1].Code.Should().Be("387517004");
        results[1].Display.Should().Be("Paracetamol");
        results[1].Fsn.Should().BeNull();
    }

    #endregion

    #region SearchAsync Error Handling Tests

    [Fact]
    public async Task SearchAsync_HttpError_ReturnsEmptyList()
    {
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.InternalServerError));

        var httpClient = new HttpClient(mockHandler.Object);
        var client = new OntoserverClient(httpClient);

        var results = await client.SearchAsync("diabetes", "http://snomed.info/sct");

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchAsync_Cancelled_ReturnsEmptyList()
    {
        var cts = new CancellationTokenSource();
        cts.Cancel();

        var client = CreateClientWithMockResponse(new { resourceType = "ValueSet" });

        var results = await client.SearchAsync("diabetes", "http://snomed.info/sct", null, cts.Token);

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchAsync_InvalidJson_ReturnsEmptyList()
    {
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("not valid json")
            });

        var httpClient = new HttpClient(mockHandler.Object);
        var client = new OntoserverClient(httpClient);

        var results = await client.SearchAsync("diabetes", "http://snomed.info/sct");

        results.Should().BeEmpty();
    }

    #endregion

    #region SearchAsync Edition Filtering Tests

    [Fact]
    public async Task SearchAsync_WithEditionUri_IncludesInRequest()
    {
        var (client, mockHandler) = CreateClientWithMockHandler();

        await client.SearchAsync("diabetes", "http://snomed.info/sct", "http://snomed.info/sct/32506021000036107");

        mockHandler.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(req =>
                req.Content != null &&
                req.Content.ReadAsStringAsync().Result.Contains("32506021000036107")),
            ItExpr.IsAny<CancellationToken>());
    }

    #endregion

    #region SearchAsync Empty Expansion Tests

    [Fact]
    public async Task SearchAsync_EmptyExpansion_ReturnsEmptyList()
    {
        var response = new
        {
            resourceType = "ValueSet",
            expansion = new
            {
                total = 0,
                contains = Array.Empty<object>()
            }
        };

        var client = CreateClientWithMockResponse(response);

        var results = await client.SearchAsync("nonexistent", "http://snomed.info/sct");

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchAsync_NullExpansion_ReturnsEmptyList()
    {
        var response = new
        {
            resourceType = "ValueSet"
        };

        var client = CreateClientWithMockResponse(response);

        var results = await client.SearchAsync("test", "http://snomed.info/sct");

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchAsync_NullContains_ReturnsEmptyList()
    {
        var response = new
        {
            resourceType = "ValueSet",
            expansion = new
            {
                total = 0
            }
        };

        var client = CreateClientWithMockResponse(response);

        var results = await client.SearchAsync("test", "http://snomed.info/sct");

        results.Should().BeEmpty();
    }

    #endregion

    #region Helper Methods

    private static OntoserverClient CreateClientWithMockResponse(object response)
    {
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(response))
            });

        var httpClient = new HttpClient(mockHandler.Object);
        return new OntoserverClient(httpClient);
    }

    private static (OntoserverClient Client, Mock<HttpMessageHandler> MockHandler) CreateClientWithMockHandler()
    {
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(new
                {
                    resourceType = "ValueSet",
                    expansion = new
                    {
                        total = 0,
                        contains = Array.Empty<object>()
                    }
                }))
            });

        var httpClient = new HttpClient(mockHandler.Object);
        var client = new OntoserverClient(httpClient);
        return (client, mockHandler);
    }

    #endregion
}

/// <summary>
/// Tests for batch lookup functionality on OntoserverClient.
/// </summary>
public class OntoserverClientBatchLookupTests
{
    [Fact]
    public async Task BatchLookupAsync_EmptyInput_ReturnsEmptyResult()
    {
        var client = CreateClientWithMockResponse(new { resourceType = "ValueSet" });

        var result = await client.BatchLookupAsync(Array.Empty<string>());

        result.PtByCode.Should().BeEmpty();
        result.FsnByCode.Should().BeEmpty();
        result.ActiveByCode.Should().BeEmpty();
    }

    [Fact]
    public async Task BatchLookupAsync_DuplicateIds_DeduplicatesInput()
    {
        var (client, mockHandler) = CreateClientWithMockHandler();

        await client.BatchLookupAsync(new[] { "73211009", "73211009", "73211009" });

        mockHandler.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task BatchLookupAsync_ParsesResults_Correctly()
    {
        var response = new
        {
            resourceType = "ValueSet",
            expansion = new
            {
                total = 1,
                contains = new[]
                {
                    new
                    {
                        system = "http://snomed.info/sct",
                        code = "73211009",
                        display = "Diabetes mellitus",
                        inactive = false,
                        designation = new[]
                        {
                            new
                            {
                                use = new { system = "http://snomed.info/sct", code = "900000000000003001" },
                                value = "Diabetes mellitus (disorder)"
                            }
                        }
                    }
                }
            }
        };

        var client = CreateClientWithMockResponse(response);

        var result = await client.BatchLookupAsync(new[] { "73211009" });

        result.PtByCode.Should().ContainKey("73211009");
        result.PtByCode["73211009"].Should().Be("Diabetes mellitus");
        result.FsnByCode.Should().ContainKey("73211009");
        result.FsnByCode["73211009"].Should().Be("Diabetes mellitus (disorder)");
        result.ActiveByCode.Should().ContainKey("73211009");
        result.ActiveByCode["73211009"].Should().BeTrue();
    }

    [Fact]
    public async Task BatchLookupAsync_InactiveConcept_ParsesCorrectly()
    {
        var response = new
        {
            resourceType = "ValueSet",
            expansion = new
            {
                total = 1,
                contains = new[]
                {
                    new
                    {
                        system = "http://snomed.info/sct",
                        code = "12345678",
                        display = "Inactive concept",
                        inactive = true,
                        designation = (object[]?)null
                    }
                }
            }
        };

        var client = CreateClientWithMockResponse(response);

        var result = await client.BatchLookupAsync(new[] { "12345678" });

        result.ActiveByCode["12345678"].Should().BeFalse();
    }

    [Fact]
    public async Task BatchLookupAsync_HttpError_ReturnsEmptyResult()
    {
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.InternalServerError));

        var httpClient = new HttpClient(mockHandler.Object);
        var client = new OntoserverClient(httpClient);

        var result = await client.BatchLookupAsync(new[] { "73211009" });

        result.PtByCode.Should().BeEmpty();
        result.FsnByCode.Should().BeEmpty();
        result.ActiveByCode.Should().BeEmpty();
    }

    [Fact]
    public async Task BatchLookupAsync_Cancelled_ReturnsEmptyResult()
    {
        var cts = new CancellationTokenSource();
        cts.Cancel();

        var client = CreateClientWithMockResponse(new { resourceType = "ValueSet" });

        var result = await client.BatchLookupAsync(new[] { "73211009" }, cts.Token);

        result.PtByCode.Should().BeEmpty();
    }

    #region Helper Methods

    private static OntoserverClient CreateClientWithMockResponse(object response)
    {
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(response))
            });

        var httpClient = new HttpClient(mockHandler.Object);
        return new OntoserverClient(httpClient);
    }

    private static (OntoserverClient Client, Mock<HttpMessageHandler> MockHandler) CreateClientWithMockHandler()
    {
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(new
                {
                    resourceType = "ValueSet",
                    expansion = new
                    {
                        total = 0,
                        contains = Array.Empty<object>()
                    }
                }))
            });

        var httpClient = new HttpClient(mockHandler.Object);
        var client = new OntoserverClient(httpClient);
        return (client, mockHandler);
    }

    #endregion
}
