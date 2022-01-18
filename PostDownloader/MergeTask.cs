using System.Text.Json;

/// <summary>
/// Task to merge all post files.
/// </summary>
public class MergeTask
{
	private string workingFolder;
	private record Post(uint id, string title, string body);

	private MergeTask() {}

	public static MergeTask Create(string workingFolder)
	{
		var task = new MergeTask();
		task.workingFolder = workingFolder;
		return task;
	}

	public async Task RunAsync()
	{
		var posts = new List<Post>();

		foreach (var file in Directory.GetFiles(workingFolder, "Post_*.json"))
		{
			var fileName = Path.GetFileName(file);

			Console.WriteLine($"Found post file: {fileName}");

			try
			{
				using var stream = File.OpenRead(file);
				var post = JsonSerializer.Deserialize<Post>(stream);
				posts.Add(post);
			}
			catch
			{
				Console.WriteLine($"Skipped merging {fileName} due to an exception.");
			}
		}

		var merged = JsonSerializer.Serialize<List<Post>>(posts);

		var jsonFile = $"merged.json";
		var jsonFilePath = Path.Combine(workingFolder, jsonFile);

		await File.WriteAllTextAsync(jsonFilePath, merged);
		Console.WriteLine($"Merged all posts to {jsonFile}");
	}
}