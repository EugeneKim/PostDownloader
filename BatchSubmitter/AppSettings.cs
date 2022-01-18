public class AppSettings
{
	/// <summary>
	/// Post IDs.
	/// </summary>
	/// <remarks>
	/// JSON PlaceHolder supports up to 100 posts.
	/// </remarks>	
	public int[] PostIds { get; init; }

	/// <summary>
	/// True to delete the batch job after completion, false to keep.
	/// </summary>
	public bool ShouldDeleteBatchJob { get; init; }

	/// <summary>
	/// True to delete the batch pool after completion. false to keep.
	/// </summary>
	public bool ShouldDeleteBatchPool { get; init; }

	/// <summary>
	/// Azure container registry settings.
	/// </summary>
	public Acr Acr { get; init; }

	/// <summary>
	/// Azure batch service settings.
	/// </summary>
	public Batch Batch { get; init; }

	/// <summary>
	/// Azure storage account settings.
	/// </summary>
	public StorageAccount StorageAccount { get; init; }
}

public class Acr
{
	/// <summary>
	/// User name to log into the registry server.
	/// </summary>
	public string UserName { get; init; }

	/// <summary>
	/// Password to log into the registry server.
	/// </summary>
	public string Password { get; init; }

	/// <summary>
	/// Container registry Url.
	/// </summary>
	public string RegistryServer { get; init; }

	/// <summary>
	/// Container image name.
	/// </summary>
	/// <remarks>
	/// Image name can include the tag if required.
	/// </remarks>
	public string imageName { get; init; }
}

public class Batch
{
	/// <summary>
	/// Account name.
	/// </summary>
	public string AccountName { get; init; }

	/// <summary>
	/// Account key.
	/// </summary>
	public string AccountKey { get; init; }

	/// <summary>
	/// Batch service endpoint.
	/// </summary>
	public string ServiceUrl { get; init; }

	/// <summary>
	/// Pool ID.
	/// </summary>
	public string PoolId { get; init; }

	/// <summary>
	/// Size of the virtuam machine in the pool.
	/// </summary>
	public string VmSize { get; init; }

	/// <summary>
	/// Number of dedicated compute nodes in the pool.
	/// </summary>
	public int DedicatedNodes { get; init; }

	/// <summary>
	/// Job ID.
	/// </summary>
	public string JobId { get; init; }
}

public class StorageAccount 
{
	/// <summary>
	/// Account name.
	/// </summary>
	public string AccountName { get; init; }

	/// <summary>
	/// Account key.
	/// </summary>
	public string AccountKey { get; init; }

	/// <summary>
	/// Name of the blob container.
	/// </summary>
	public string ContainerName {get; init; }
}