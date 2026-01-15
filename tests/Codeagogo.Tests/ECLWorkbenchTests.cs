// Copyright 2026 CSIRO. Licensed under the Apache License, Version 2.0.
// SPDX-License-Identifier: Apache-2.0

using System.IO;
using FluentAssertions;

namespace Codeagogo.Tests;

/// <summary>
/// Tests for ECL Workbench supporting components.
/// </summary>
public class ECLWorkbenchTests
{
    #region ECLEditorHtmlBuilder

    [Fact]
    public void Build_DefaultParameters_ContainsEditorElement()
    {
        var html = ECLEditorHtmlBuilder.Build();
        html.Should().Contain("<ecl-editor");
    }

    [Fact]
    public void Build_DefaultParameters_ContainsMonacoCdn()
    {
        var html = ECLEditorHtmlBuilder.Build();
        html.Should().Contain("monaco-editor@0.52.2");
    }

    [Fact]
    public void Build_WithValue_EscapesHtml()
    {
        var html = ECLEditorHtmlBuilder.Build(value: "<< 404684003 & \"test\"");
        html.Should().Contain("&lt;&lt; 404684003 &amp; &quot;test&quot;");
        html.Should().NotContain("value=\"<< 404684003");
    }

    [Fact]
    public void Build_WithFhirUrl_IncludesUrl()
    {
        var html = ECLEditorHtmlBuilder.Build(fhirServerUrl: "https://tx.example.com/fhir");
        html.Should().Contain("https://tx.example.com/fhir");
    }

    [Fact]
    public void Build_DarkTheme_UsesVsDark()
    {
        var html = ECLEditorHtmlBuilder.Build(darkTheme: true);
        html.Should().Contain("vs-dark");
    }

    [Fact]
    public void Build_LightTheme_UsesVs()
    {
        var html = ECLEditorHtmlBuilder.Build(darkTheme: false);
        html.Should().Contain("theme=\"vs\"");
        html.Should().NotContain("vs-dark");
    }

    [Fact]
    public void Build_ContainsVirtualHostReference()
    {
        var html = ECLEditorHtmlBuilder.Build();
        html.Should().Contain(ECLEditorHtmlBuilder.VirtualHost);
    }

    [Fact]
    public void Build_ContainsWebView2PostMessage()
    {
        var html = ECLEditorHtmlBuilder.Build();
        html.Should().Contain("window.chrome.webview.postMessage");
    }

    [Fact]
    public void Build_ContainsChangeEvent()
    {
        var html = ECLEditorHtmlBuilder.Build();
        html.Should().Contain("event: 'change'");
    }

    [Fact]
    public void Build_ContainsEvaluateEvent()
    {
        var html = ECLEditorHtmlBuilder.Build();
        html.Should().Contain("event: 'evaluate'");
    }

    [Fact]
    public void Build_ContainsDebounce()
    {
        var html = ECLEditorHtmlBuilder.Build();
        html.Should().Contain("500"); // 500ms debounce
    }

    [Fact]
    public void Build_ContainsCtrlEnterHandler()
    {
        var html = ECLEditorHtmlBuilder.Build();
        html.Should().Contain("e.ctrlKey");
        html.Should().Contain("Enter");
    }

    [Fact]
    public void Build_ContainsCdnTimeout()
    {
        var html = ECLEditorHtmlBuilder.Build();
        html.Should().Contain("10000"); // 10-second timeout
    }

    #endregion

    #region ECLEditorResourceManager

    [Fact]
    public void GetResourceDirectory_ReturnsExistingDirectory()
    {
        var dir = ECLEditorResourceManager.GetResourceDirectory();
        Directory.Exists(dir).Should().BeTrue();
    }

    [Fact]
    public void GetResourceDirectory_ContainsBundle()
    {
        var dir = ECLEditorResourceManager.GetResourceDirectory();
        var bundlePath = Path.Combine(dir, "ecl-editor.standalone.js");
        File.Exists(bundlePath).Should().BeTrue();
    }

    [Fact]
    public void GetResourceDirectory_IdempotentCalls()
    {
        var dir1 = ECLEditorResourceManager.GetResourceDirectory();
        var dir2 = ECLEditorResourceManager.GetResourceDirectory();
        dir1.Should().Be(dir2);
    }

    #endregion

    #region ECLWorkbenchViewModel

    [Fact]
    public async Task EvaluateAsync_EmptyExpression_ClearsResults()
    {
        var vm = new ECLWorkbenchViewModel(new OntoserverClient(baseUrl: "https://test.example.com/fhir/"));
        await vm.EvaluateAsync("");

        vm.Result.Should().BeNull();
        vm.ErrorMessage.Should().BeNull();
        vm.IsEvaluating.Should().BeFalse();
    }

    [Fact]
    public void ViewModel_DefaultState()
    {
        var vm = new ECLWorkbenchViewModel(new OntoserverClient(baseUrl: "https://test.example.com/fhir/"));

        vm.CurrentExpression.Should().BeEmpty();
        vm.IsEvaluating.Should().BeFalse();
        vm.Result.Should().BeNull();
        vm.ErrorMessage.Should().BeNull();
        vm.ShowFsn.Should().BeFalse();
    }

    [Fact]
    public void ViewModel_ShowFsn_RaisesPropertyChanged()
    {
        var vm = new ECLWorkbenchViewModel(new OntoserverClient(baseUrl: "https://test.example.com/fhir/"));
        var changed = new List<string>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        vm.ShowFsn = true;

        changed.Should().Contain(nameof(ECLWorkbenchViewModel.ShowFsn));
    }

    #endregion
}
