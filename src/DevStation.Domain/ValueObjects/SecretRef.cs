namespace DevStation.Domain.ValueObjects;

/// <summary>Key into ISecretStore — never the secret value itself.</summary>
public record SecretRef(string Key);
