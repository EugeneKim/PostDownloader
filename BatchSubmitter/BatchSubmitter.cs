using System.Diagnostics;
using Azure.Storage.Blobs;
using Azure.Storage.Sas;
using Microsoft.Azure.Batch;
using Microsoft.Azure.Batch.Auth;
using Microsoft.Azure.Batch.Common;
using Microsoft.Extensions.Configuration;

public class BatchSubmitter
{
	private AppSettings appSettings;

	public BatchSubmitter() 
	{
		appSettings = new ConfigurationBuilder()
			.AddJsonFile("appsettings.json")
			.AddEnvironmentVariables()
			.Build()
			.Get<AppSettings>();
	}

	public async Task RunAsync()
	{
		// Create a client to manipulate Azure storage container and blobs.

        var blobServiceClient = new BlobServiceClient(GetConnectionString());
    	var blobContainerClient = blobServiceClient.GetBlobContainerClient(appSettings.StorageAccount.ContainerName);

    	await blobContainerClient.CreateIfNotExistsAsync();

		var blobContainerSasUri = GetBlobContainerSasUri(blobContainerClient);

		// Create a client to manupilate Azure batch.

		var batchSharedKeyCredentials = new BatchSharedKeyCredentials(
			appSettings.Batch.ServiceUrl,
			appSettings.Batch.AccountName,
			appSettings.Batch.AccountKey);

		using (var batchClient = BatchClient.Open(batchSharedKeyCredentials))
		{
			// Configure the VM with the container image in ACR.

			var imageReference =  new ImageReference(
				"ubuntu-server-container",
				"microsoft-azure-batch",
                "20-04-lts",
                "latest");

			var containerRegistry = new ContainerRegistry(
				appSettings.Acr.UserName,
				appSettings.Acr.Password,
				appSettings.Acr.RegistryServer);

            var containerConfig = new ContainerConfiguration();
			var containerImageName = $"{appSettings.Acr.RegistryServer}/{appSettings.Acr.imageName}";
            containerConfig.ContainerImageNames = new List<string> { containerImageName };
            containerConfig.ContainerRegistries = new List<ContainerRegistry> { containerRegistry };

			var vmConfig = new VirtualMachineConfiguration(
                imageReference,
                "batch.node.ubuntu 20.04");
			vmConfig.ContainerConfiguration = containerConfig;

			// Create the batch pool.

			await CreatePoolIfNotExistAsync(
				batchClient,
				vmConfig,
				appSettings.Batch.DedicatedNodes,
				0,
				1,
				appSettings.Batch.VmSize,
				appSettings.Batch.PoolId);

			try
			{
				// Create the job and tasks.

				await CreateJobIfNotExistAsync(
					batchClient,
					appSettings.Batch.JobId,
					appSettings.Batch.PoolId,
					true,
					0);

				var postDownloadTasks = CreatePostDownloadTasks(appSettings.PostIds, blobContainerSasUri, containerImageName);
				var tasksToAdd = new List<CloudTask>(postDownloadTasks);

				var mergeTask = CreateMergeTask(blobContainerSasUri, blobContainerClient, containerImageName, postDownloadTasks);				
				tasksToAdd.Add(mergeTask);
				
				await batchClient.JobOperations.AddTaskAsync(appSettings.Batch.JobId, tasksToAdd);

				var boundJob = await batchClient.JobOperations.GetJobAsync(appSettings.Batch.JobId);
				boundJob.OnAllTasksComplete = OnAllTasksComplete.TerminateJob;
				boundJob.Commit();
				boundJob.Refresh();

				// Wait for the batch job done.

				var addedTasks = await batchClient.JobOperations.ListTasks(appSettings.Batch.JobId).ToListAsync();

				var taskStateMonitor = batchClient.Utilities.CreateTaskStateMonitor();

				var stopWatch = new Stopwatch();
				stopWatch.Start();
				Console.WriteLine("Waiting for all tasks done...");

				await taskStateMonitor.WhenAll(addedTasks, TaskState.Completed, TimeSpan.FromMinutes(30));

				stopWatch.Stop();
				Console.WriteLine($"All tasks done. Elapsed time: {stopWatch.Elapsed}");
			}
			finally
			{
				if (appSettings.ShouldDeleteBatchPool)
				{
					Console.WriteLine($"Deleting pool({appSettings.Batch.PoolId})...");
					await batchClient.PoolOperations.DeletePoolAsync(appSettings.Batch.PoolId);
				}

				if (appSettings.ShouldDeleteBatchPool)
				{
					Console.WriteLine($"Deleting job({appSettings.Batch.JobId})...");
					await batchClient.JobOperations.DeleteJobAsync(appSettings.Batch.JobId);
				}
			}
		}
	}

	/// <summary>
	/// Create the Azure batch pool if not created already.
	/// </summary>
	private async Task CreatePoolIfNotExistAsync(BatchClient batchClient, VirtualMachineConfiguration vmConfig, int dedicatedNodes, int lowPriorityNodes, int taskSlotsPerNode, string vmSize, string poolId)
	{
		try
		{
			var pool = batchClient.PoolOperations.CreatePool(
				poolId,
				vmSize,
				vmConfig,
				dedicatedNodes,
				lowPriorityNodes);

			pool.TaskSlotsPerNode = taskSlotsPerNode;

			await pool.CommitAsync();
		}
		catch (BatchException ex) when (ex.RequestInformation.BatchError.Code != BatchErrorCodeStrings.PoolExists)
		{
			throw;
		}
	}

	/// <summary>
	/// Create the Azure batch job if not created already.
	/// </summary>
	private async Task CreateJobIfNotExistAsync(BatchClient batchClient, string jobId, string poolId, bool usesTaskDependencies, int priority)
	{
		try
		{
			var job = batchClient.JobOperations.CreateJob();

			job.Id = jobId;
			job.UsesTaskDependencies = usesTaskDependencies;
			job.PoolInformation = new PoolInformation() { PoolId = poolId };
			job.Priority = priority;

			await job.CommitAsync();
		}
		catch (BatchException ex) when (ex.RequestInformation.BatchError.Code != BatchErrorCodeStrings.JobExists)
		{
			throw;
		}
	}

	/// <summary>
	/// Create the Azure batch tasks that download posts.
	/// </summary>
	private IReadOnlyList<CloudTask> CreatePostDownloadTasks(int[] postIds, string blobContainerSasUri, string containerImageName)
	{
		var tasks = new List<CloudTask>();

		foreach(var postId in postIds)
		{
			var taskId = $"posttask{postId}";
			var commandLine = $"/bin/sh -c 'dotnet /app/PostDownloader.dll /post:{postId} $AZ_BATCH_TASK_WORKING_DIR'";
			var task = new CloudTask(taskId, commandLine);
			var fileName = $"Post_{postId}.json";

			task.OutputFiles = new List<OutputFile>
			{
				new OutputFile(
					filePattern: fileName,
					destination: new OutputFileDestination(
						container: new OutputFileBlobContainerDestination(blobContainerSasUri, fileName)),
					uploadOptions: new OutputFileUploadOptions(OutputFileUploadCondition.TaskSuccess))
			};

			task.ContainerSettings = new TaskContainerSettings(imageName: containerImageName, containerRunOptions: "--rm");
			tasks.Add(task);
		}

		return tasks;
	}

	/// <summary>
	/// Create the cloud task to merge the posts.
	/// </summary>
	private CloudTask CreateMergeTask(string blobContainerSasUri, BlobContainerClient blobContainerClient, string containerImageName, IReadOnlyList<CloudTask> postTasks)
	{
		var resourceFiles = new List<ResourceFile>();

		foreach (var postTask in postTasks)
			resourceFiles.Add(GetResourceFile(blobContainerClient, postTask.OutputFiles[0].Destination.Container.Path));

		var task = new CloudTask("mergetask", "/bin/sh -c 'dotnet /app/PostDownloader.dll /merge $AZ_BATCH_TASK_WORKING_DIR'");
		var fileName = "merged.json";

		task.OutputFiles = new List<OutputFile>
		{
			new OutputFile(
				filePattern: fileName,
				destination: new OutputFileDestination(
					container: new OutputFileBlobContainerDestination(blobContainerSasUri, fileName)),
				uploadOptions: new OutputFileUploadOptions(OutputFileUploadCondition.TaskSuccess))
		};

		task.ContainerSettings = new TaskContainerSettings(imageName: containerImageName, containerRunOptions: "--rm");
		task.ResourceFiles = resourceFiles;
		task.DependsOn = TaskDependencies.OnTasks(postTasks);
		return task;
	}

	/// <summary>
	/// Get the connection string of storage account.
	/// </summary>
	private string GetConnectionString() => $"DefaultEndpointsProtocol=https;AccountName={appSettings.StorageAccount.AccountName};" +
			$"AccountKey={appSettings.StorageAccount.AccountKey}" +
			"EndpointSuffix=core.windows.net";

	/// <summary>
	/// Get the resoure file of the blob in the blob container.
	/// </summary>
	private static ResourceFile GetResourceFile(BlobContainerClient blobContainerClient, string blobName)
    {
		if (!blobContainerClient.CanGenerateSasUri)
			throw new NotSupportedException("Ensure that the blob container can generate a SAS.");

        var sasBuilder = new BlobSasBuilder()
        {
            BlobContainerName = blobContainerClient.Name,
			BlobName = blobName,
            Resource = "b",
			ExpiresOn = DateTimeOffset.UtcNow.AddHours(1)
        };

		sasBuilder.SetPermissions(BlobContainerSasPermissions.Read);

		var blobClient = blobContainerClient.GetBlobClient(blobName);
		var sasUri = blobClient.GenerateSasUri(sasBuilder);

        return ResourceFile.FromUrl(sasUri.AbsoluteUri, blobName);
    }

	/// <summary>
	/// Get SAS URI as of the blob container a string.
	/// </summary>
	private static string GetBlobContainerSasUri(BlobContainerClient blobContainerClient)
	{
		if (!blobContainerClient.CanGenerateSasUri)
			throw new NotSupportedException("Ensure that the blob container can generate a SAS.");

        var sasBuilder = new BlobSasBuilder()
        {
            BlobContainerName = blobContainerClient.Name,
            Resource = "c",
			ExpiresOn = DateTimeOffset.UtcNow.AddHours(1)
        };

		sasBuilder.SetPermissions(BlobContainerSasPermissions.Read | BlobContainerSasPermissions.Write);

        return blobContainerClient.GenerateSasUri(sasBuilder).AbsoluteUri;
	}
}