namespace RetailStorePOS.Data.Modules.Contracts;

public enum OwnedDataAssetType
{
    Table,
    Aggregate,
    ReadModel,
    OperationalState
}

public sealed record OwnedDataAsset
{
    public string AssetKey { get; init; } = string.Empty;
    public OwnedDataAssetType AssetType { get; init; }
    public string OwnerModuleKey { get; init; } = string.Empty;
    public IReadOnlyList<string> PrimaryConsumers { get; init; } = [];
    public string Notes { get; init; } = string.Empty;
}
