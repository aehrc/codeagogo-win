// Copyright 2026 CSIRO. Licensed under the Apache License, Version 2.0.
// SPDX-License-Identifier: Apache-2.0

using FluentAssertions;

namespace Codeagogo.Tests;

/// <summary>
/// Tests for the TextSelectionHelper that simulates text selection via SendInput.
/// Note: SendInput may not produce the expected key events in a test environment,
/// but these tests verify that the methods handle edge cases correctly
/// and do not throw exceptions.
/// </summary>
public class TextSelectionHelperTests
{
    #region SelectInsertedText

    [Fact]
    public void SelectInsertedText_LengthZero_ReturnsFalse()
    {
        var result = TextSelectionHelper.SelectInsertedText(0);

        result.Should().BeFalse("length of 0 means no text to select");
    }

    [Fact]
    public void SelectInsertedText_NegativeLength_ReturnsFalse()
    {
        var result = TextSelectionHelper.SelectInsertedText(-1);

        result.Should().BeFalse("negative length is invalid");
    }

    [Fact]
    public void SelectInsertedText_NegativeLargeLength_ReturnsFalse()
    {
        var result = TextSelectionHelper.SelectInsertedText(-100);

        result.Should().BeFalse("large negative length is invalid");
    }

    [Fact]
    public void SelectInsertedText_PositiveLength_DoesNotThrow()
    {
        // In a test environment, SendInput may not produce key events,
        // but it should not throw an exception.
        var act = () => TextSelectionHelper.SelectInsertedText(5);

        act.Should().NotThrow();
    }

    [Fact]
    public void SelectInsertedText_LargeLength_DoesNotThrow()
    {
        var act = () => TextSelectionHelper.SelectInsertedText(1000);

        act.Should().NotThrow();
    }

    #endregion

    #region SendShiftLeft

    [Fact]
    public void SendShiftLeft_DoesNotThrow()
    {
        // SendShiftLeft uses SendInput which may not work in test,
        // but should not throw.
        var act = () => TextSelectionHelper.SendShiftLeft();

        act.Should().NotThrow();
    }

    #endregion
}
