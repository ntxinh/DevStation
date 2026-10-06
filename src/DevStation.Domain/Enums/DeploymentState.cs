namespace DevStation.Domain.Enums;

// kept distinct from DeploymentStatus: mirrors the external provider's state vocabulary
public enum DeploymentState { Queued, Running, Succeeded, Failed, Cancelled }
