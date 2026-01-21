namespace SNOMEDLookup.Tests;

public class ConceptResultTests
{
    [Fact]
    public void ActiveText_ReturnsActive_WhenActiveIsTrue()
    {
        var result = new ConceptResult(
            ConceptId: "12345",
            Branch: "MAIN",
            Fsn: "Test (finding)",
            Pt: "Test",
            Active: true,
            EffectiveTime: null,
            ModuleId: null
        );

        Assert.Equal("active", result.ActiveText);
    }

    [Fact]
    public void ActiveText_ReturnsInactive_WhenActiveIsFalse()
    {
        var result = new ConceptResult(
            ConceptId: "12345",
            Branch: "MAIN",
            Fsn: "Test (finding)",
            Pt: "Test",
            Active: false,
            EffectiveTime: null,
            ModuleId: null
        );

        Assert.Equal("inactive", result.ActiveText);
    }

    [Fact]
    public void ActiveText_ReturnsDash_WhenActiveIsNull()
    {
        var result = new ConceptResult(
            ConceptId: "12345",
            Branch: "MAIN",
            Fsn: "Test (finding)",
            Pt: "Test",
            Active: null,
            EffectiveTime: null,
            ModuleId: null
        );

        Assert.Equal("-", result.ActiveText);
    }

    [Fact]
    public void Record_StoresAllProperties()
    {
        var result = new ConceptResult(
            ConceptId: "73211009",
            Branch: "MAIN/SNOMEDCT-AU",
            Fsn: "Diabetes mellitus (disorder)",
            Pt: "Diabetes mellitus",
            Active: true,
            EffectiveTime: "20230101",
            ModuleId: "32506021000036107",
            Edition: "Australian"
        );

        Assert.Equal("73211009", result.ConceptId);
        Assert.Equal("MAIN/SNOMEDCT-AU", result.Branch);
        Assert.Equal("Diabetes mellitus (disorder)", result.Fsn);
        Assert.Equal("Diabetes mellitus", result.Pt);
        Assert.True(result.Active);
        Assert.Equal("20230101", result.EffectiveTime);
        Assert.Equal("32506021000036107", result.ModuleId);
        Assert.Equal("Australian", result.Edition);
    }

    [Fact]
    public void Edition_DefaultsToNull()
    {
        var result = new ConceptResult(
            ConceptId: "12345",
            Branch: "MAIN",
            Fsn: null,
            Pt: null,
            Active: null,
            EffectiveTime: null,
            ModuleId: null
        );

        Assert.Null(result.Edition);
    }

    [Fact]
    public void Records_WithSameValues_AreEqual()
    {
        var result1 = new ConceptResult(
            ConceptId: "12345",
            Branch: "MAIN",
            Fsn: "Test",
            Pt: "Test",
            Active: true,
            EffectiveTime: "20230101",
            ModuleId: "900000000000207008"
        );

        var result2 = new ConceptResult(
            ConceptId: "12345",
            Branch: "MAIN",
            Fsn: "Test",
            Pt: "Test",
            Active: true,
            EffectiveTime: "20230101",
            ModuleId: "900000000000207008"
        );

        Assert.Equal(result1, result2);
    }

    [Fact]
    public void Records_WithDifferentValues_AreNotEqual()
    {
        var result1 = new ConceptResult(
            ConceptId: "12345",
            Branch: "MAIN",
            Fsn: "Test",
            Pt: "Test",
            Active: true,
            EffectiveTime: "20230101",
            ModuleId: "900000000000207008"
        );

        var result2 = new ConceptResult(
            ConceptId: "54321",
            Branch: "MAIN",
            Fsn: "Test",
            Pt: "Test",
            Active: true,
            EffectiveTime: "20230101",
            ModuleId: "900000000000207008"
        );

        Assert.NotEqual(result1, result2);
    }
}
