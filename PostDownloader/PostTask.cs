/// <summary>
/// Task to download a post from the fake REST API.
/// </summary>
public class PostTask
{
	private uint postId;
	private string workingFolder;

	private PostTask() {}

	public static PostTask Create(uint postId, string workingFolder)
	{
		var task = new PostTask();
		
		task.postId = postId;
		task.workingFolder = workingFolder;

		return task;
	}

	public async Task RunAsync()
	{
		var uri = $"https://jsonplaceholder.typicode.com/posts/{postId}";
		
		var httpClient = new HttpClient();
		var response = await httpClient.GetStringAsync(uri);

		var jsonFile = Path.Combine(workingFolder, $"Post_{postId}.json");
		await File.WriteAllTextAsync(jsonFile, response);

		Console.WriteLine($"Post saved to {jsonFile}");
	}
}