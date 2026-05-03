namespace RetailStorePOS.Data.Modules.Contracts;

public enum ModuleContractType
{
    Command,
    Query,
    Event
}

public sealed record ModuleContract
{
    public string ContractKey { get; init; } = string.Empty;
    public string OwnerModuleKey { get; init; } = string.Empty;
    public ModuleContractType ContractType { get; init; }
    public string Purpose { get; init; } = string.Empty;
    public IReadOnlyList<string> Consumers { get; init; } = [];
    public bool OfflineAllowed { get; init; }
}
