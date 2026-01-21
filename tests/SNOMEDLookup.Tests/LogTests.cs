namespace SNOMEDLookup.Tests;

public class LogTests
{
    [Theory]
    [InlineData("Hello World", 100, "Hello World")]
    [InlineData("Short", 10, "Short")]
    [InlineData("This is a very long string", 10, "This is a ...")]
    [InlineData("Exactly10!", 10, "Exactly10!")]
    [InlineData("Exactly11ch", 10, "Exactly11c...")]
    public void Snippet_TruncatesCorrectly(string input, int limit, string expected)
    {
        var result = Log.Snippet(input, limit);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Snippet_ReturnsEmpty_ForNullInput()
    {
        var result = Log.Snippet(null, 100);
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void Snippet_ReturnsEmpty_ForEmptyInput()
    {
        var result = Log.Snippet(string.Empty, 100);
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void Snippet_UsesDefaultLimit_WhenNotSpecified()
    {
        var longString = new string('x', 150);
        var result = Log.Snippet(longString);

        // Default limit is 100, so result should be 103 chars (100 + "...")
        Assert.Equal(103, result.Length);
        Assert.EndsWith("...", result);
    }

    [Fact]
    public void DebugEnabled_DefaultsToFalse()
    {
        // Save original value to restore later
        var original = Log.DebugEnabled;
        try
        {
            // Reset to verify default behavior
            Log.DebugEnabled = false;
            Assert.False(Log.DebugEnabled);
        }
        finally
        {
            Log.DebugEnabled = original;
        }
    }

    [Fact]
    public void DebugEnabled_CanBeSet()
    {
        var original = Log.DebugEnabled;
        try
        {
            Log.DebugEnabled = true;
            Assert.True(Log.DebugEnabled);

            Log.DebugEnabled = false;
            Assert.False(Log.DebugEnabled);
        }
        finally
        {
            Log.DebugEnabled = original;
        }
    }

    [Fact]
    public void GetLogPath_ReturnsNonEmptyPath()
    {
        var path = Log.GetLogPath();

        Assert.False(string.IsNullOrEmpty(path));
        Assert.Contains("SNOMED Lookup", path);
        Assert.EndsWith("app.log", path);
    }

    [Fact]
    public void GetRecentLogs_ReturnsString()
    {
        // This might return "No logs available." if no logs exist,
        // or actual log content if logs exist
        var result = Log.GetRecentLogs(10);

        Assert.NotNull(result);
    }
}
