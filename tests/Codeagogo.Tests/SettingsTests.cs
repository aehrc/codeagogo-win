// Copyright 2026 CSIRO. Licensed under the Apache License, Version 2.0.
// SPDX-License-Identifier: Apache-2.0

using FluentAssertions;

namespace Codeagogo.Tests;

/// <summary>
/// Tests for Settings class default values, backward compatibility, and loading.
/// </summary>
public class SettingsTests
{
    #region Default Values

    [Fact]
    public void Settings_DefaultLookupHotKeyModifiers_IsCtrlShift()
    {
        var settings = new Settings();

        // MOD_CONTROL (0x0002) | MOD_SHIFT (0x0004) = 0x0006
        settings.LookupHotKeyModifiers.Should().Be(0x0006);
    }

    [Fact]
    public void Settings_DefaultLookupHotKeyVirtualKey_IsL()
    {
        var settings = new Settings();

        settings.LookupHotKeyVirtualKey.Should().Be(0x4C); // 'L'
    }

    [Fact]
    public void Settings_DefaultSearchHotKeyModifiers_IsCtrlShift()
    {
        var settings = new Settings();

        settings.SearchHotKeyModifiers.Should().Be(0x0006);
    }

    [Fact]
    public void Settings_DefaultSearchHotKeyVirtualKey_IsS()
    {
        var settings = new Settings();

        settings.SearchHotKeyVirtualKey.Should().Be(0x53); // 'S'
    }

    [Fact]
    public void Settings_DefaultReplaceHotKeyModifiers_IsCtrlShift()
    {
        var settings = new Settings();

        settings.ReplaceHotKeyModifiers.Should().Be(0x0006);
    }

    [Fact]
    public void Settings_DefaultReplaceHotKeyVirtualKey_IsR()
    {
        var settings = new Settings();

        settings.ReplaceHotKeyVirtualKey.Should().Be(0x52); // 'R'
    }

    [Fact]
    public void Settings_DefaultEclFormatHotKeyModifiers_IsCtrlShift()
    {
        var settings = new Settings();

        settings.EclFormatHotKeyModifiers.Should().Be(0x0006);
    }

    [Fact]
    public void Settings_DefaultEclFormatHotKeyVirtualKey_IsE()
    {
        var settings = new Settings();

        settings.EclFormatHotKeyVirtualKey.Should().Be(0x45); // 'E'
    }

    [Fact]
    public void Settings_DefaultShrimpHotKeyModifiers_IsCtrlShift()
    {
        var settings = new Settings();

        settings.ShrimpHotKeyModifiers.Should().Be(0x0006);
    }

    [Fact]
    public void Settings_DefaultShrimpHotKeyVirtualKey_IsB()
    {
        var settings = new Settings();

        settings.ShrimpHotKeyVirtualKey.Should().Be(0x42); // 'B'
    }

    #endregion

    #region InsertFormat Default

    [Fact]
    public void Settings_DefaultInsertFormat_IsIdPipeFSN()
    {
        var settings = new Settings();

        settings.DefaultInsertFormat.Should().Be(InsertFormat.IdPipeFSN);
    }

    #endregion

    #region ReplaceTermFormat Default

    [Fact]
    public void Settings_DefaultReplaceTermFormat_IsFSN()
    {
        var settings = new Settings();

        settings.ReplaceTermFormat.Should().Be(TermFormat.FSN);
    }

    #endregion

    #region PrefixInactive Default

    [Fact]
    public void Settings_DefaultPrefixInactive_IsTrue()
    {
        var settings = new Settings();

        settings.PrefixInactive.Should().BeTrue();
    }

    #endregion

    #region FhirBaseUrl Default

    [Fact]
    public void Settings_DefaultFhirBaseUrl_IsOntoserver()
    {
        var settings = new Settings();

        settings.FhirBaseUrl.Should().Be("https://tx.ontoserver.csiro.au/fhir/");
    }

    #endregion

    #region DebugLogging Default

    [Fact]
    public void Settings_DefaultDebugLogging_IsFalse()
    {
        var settings = new Settings();

        settings.DebugLogging.Should().BeFalse();
    }

    #endregion

    #region Backward Compatibility

    [Fact]
    public void Settings_HotKeyModifiers_MapsToLookupHotKeyModifiers_Get()
    {
        var settings = new Settings();
        settings.LookupHotKeyModifiers = 0x0008;

        settings.HotKeyModifiers.Should().Be(0x0008);
    }

    [Fact]
    public void Settings_HotKeyModifiers_MapsToLookupHotKeyModifiers_Set()
    {
        var settings = new Settings();
        settings.HotKeyModifiers = 0x000A;

        settings.LookupHotKeyModifiers.Should().Be(0x000A);
    }

    [Fact]
    public void Settings_HotKeyVirtualKey_MapsToLookupHotKeyVirtualKey_Get()
    {
        var settings = new Settings();
        settings.LookupHotKeyVirtualKey = 0x42;

        settings.HotKeyVirtualKey.Should().Be(0x42);
    }

    [Fact]
    public void Settings_HotKeyVirtualKey_MapsToLookupHotKeyVirtualKey_Set()
    {
        var settings = new Settings();
        settings.HotKeyVirtualKey = 0x42;

        settings.LookupHotKeyVirtualKey.Should().Be(0x42);
    }

    #endregion

    #region Load Returns Defaults When File Doesn't Exist

    [Fact]
    public void Settings_Load_ReturnsDefaultsWhenFileDoesNotExist()
    {
        // Settings.Load() returns new Settings() when the file doesn't exist.
        // This test calls Load and verifies it returns an object with defaults.
        // Note: It reads from the user's settings path, but in most test environments
        // the specific settings file won't exist, so we just verify the result is valid.
        var settings = Settings.Load();

        settings.Should().NotBeNull();
        // Verify some defaults are present (Load returns defaults on missing file)
        settings.FhirBaseUrl.Should().NotBeNullOrEmpty();
    }

    #endregion

    #region All Hotkey Defaults

    [Fact]
    public void Settings_AllHotKeyDefaults_AreCtrlShift()
    {
        var settings = new Settings();
        const uint ctrlShift = 0x0006; // MOD_CONTROL | MOD_SHIFT

        settings.LookupHotKeyModifiers.Should().Be(ctrlShift);
        settings.SearchHotKeyModifiers.Should().Be(ctrlShift);
        settings.ReplaceHotKeyModifiers.Should().Be(ctrlShift);
        settings.EclFormatHotKeyModifiers.Should().Be(ctrlShift);
        settings.ShrimpHotKeyModifiers.Should().Be(ctrlShift);
    }

    [Fact]
    public void Settings_AllHotKeyVirtualKeys_AreDistinct()
    {
        var settings = new Settings();

        var keys = new[]
        {
            settings.LookupHotKeyVirtualKey,
            settings.SearchHotKeyVirtualKey,
            settings.ReplaceHotKeyVirtualKey,
            settings.EclFormatHotKeyVirtualKey,
            settings.ShrimpHotKeyVirtualKey
        };

        keys.Should().OnlyHaveUniqueItems("each hotkey should use a different virtual key");
    }

    #endregion

    #region InstallId

    [Fact]
    public void Settings_DefaultInstallId_IsNullOrEmpty()
    {
        var settings = new Settings();

        settings.InstallId.Should().BeNullOrEmpty();
    }

    [Fact]
    public void Settings_InstallId_CanBeSet()
    {
        var settings = new Settings();
        var id = Guid.NewGuid().ToString();

        settings.InstallId = id;

        settings.InstallId.Should().Be(id);
    }

    [Fact]
    public void Settings_ResetInstallId_GeneratesNewGuid()
    {
        var settings = new Settings();
        var previous = settings.InstallId;

        settings.ResetInstallId();

        settings.InstallId.Should().NotBeNullOrEmpty();
        settings.InstallId.Should().NotBe(previous);
        Guid.TryParse(settings.InstallId, out _).Should().BeTrue();
    }

    [Fact]
    public void Settings_ResetInstallId_GeneratesUniqueIds()
    {
        var settings = new Settings();

        settings.ResetInstallId();
        var first = settings.InstallId;

        settings.ResetInstallId();
        var second = settings.InstallId;

        first.Should().NotBe(second);
    }

    [Fact]
    public void Settings_InstallId_IsValidGuidFormat()
    {
        var settings = new Settings();
        settings.InstallId = Guid.NewGuid().ToString();

        Guid.TryParse(settings.InstallId, out _).Should().BeTrue();
    }

    #endregion

    #region WelcomeShown

    [Fact]
    public void Settings_DefaultWelcomeShown_IsFalse()
    {
        var settings = new Settings();

        settings.WelcomeShown.Should().BeFalse();
    }

    [Fact]
    public void Settings_WelcomeShown_CanBeSet()
    {
        var settings = new Settings();

        settings.WelcomeShown = true;

        settings.WelcomeShown.Should().BeTrue();
    }

    #endregion

    #region Enum Values

    [Fact]
    public void TermFormat_HasExpectedValues()
    {
        Enum.GetValues<TermFormat>().Should().Contain(TermFormat.FSN);
        Enum.GetValues<TermFormat>().Should().Contain(TermFormat.PT);
    }

    [Fact]
    public void InsertFormat_HasExpectedValues()
    {
        Enum.GetValues<InsertFormat>().Should().Contain(InsertFormat.IdOnly);
        Enum.GetValues<InsertFormat>().Should().Contain(InsertFormat.PtOnly);
        Enum.GetValues<InsertFormat>().Should().Contain(InsertFormat.FsnOnly);
        Enum.GetValues<InsertFormat>().Should().Contain(InsertFormat.IdPipePT);
        Enum.GetValues<InsertFormat>().Should().Contain(InsertFormat.IdPipeFSN);
    }

    #endregion
}
