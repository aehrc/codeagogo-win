namespace SNOMEDLookup;

public sealed record ConceptResult(
    string ConceptId,
    string Branch,
    string? Fsn,
    string? Pt,
    bool? Active,
    string? EffectiveTime,
    string? ModuleId
)
{
    public string ActiveText => Active switch
    {
        true => "active",
        false => "inactive",
        _ => "-"
    };
}
