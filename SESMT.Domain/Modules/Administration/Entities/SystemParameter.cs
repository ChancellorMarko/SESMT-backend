using System.Net.Http.Json;
using SESMT.Domain.Common.AggregateRoot;

namespace SESMT.Domain.Modules.Administration.Entities;

public class SystemParameter : AggregateRootBase
{
    public string Key { get; private set; } = null!;
    public string Value { get; private set; } = null!;
    public string Description { get; private set; } = null!;
    public string NormativeReference { get; private set; } = null!;
    public int Version { get; private set; }
    public DateTime ValidityIncialDate { get; private set; }
    public DateTime ValidityFinalDate { get; private set; }


    protected SystemParameter() { }

    public SystemParameter(string key, string value, string description, string normativeReference, int version, DateTime validityIncialDate, DateTime validityFinalDate)
    {
        Key = key;
        Value = value;
        Description = description;
        NormativeReference = normativeReference;
        Version = version;
        ValidityIncialDate = validityIncialDate;
        ValidityFinalDate = validityFinalDate;
    }

    public void UpdateParameter(string key, string value, string description, string normativeReference, int version, DateTime validityIncialDate, DateTime validityFinalDate)
    {
        Key = key;
        Value = value;
        Description = description;
        NormativeReference = normativeReference;
        Version = version;
        ValidityIncialDate = validityIncialDate;
        ValidityFinalDate = validityFinalDate;

        MarkAsUpdated();
    }
}