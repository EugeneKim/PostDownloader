using System.Text.RegularExpressions;

if (args != null && args.Length == 2)
{
	var match = Regex.Match(args[0], @"^((?<TYPE>/post):(?<POSTID>\d+)|(?<TYPE>/merge))$");

	if (match.Success)
	{
		var type = match.Result("${TYPE}");
		var workingFolder = args[1];

		if (type == "/post")
		{
			var postTask = PostTask.Create(Convert.ToUInt32(match.Result("${POSTID}")), workingFolder);	
			await postTask.RunAsync();
		}
		else if (type == "/merge")
		{
			var mergeTask = MergeTask.Create(workingFolder);
			await mergeTask.RunAsync();
		}
	}
	else
		Console.WriteLine($"Invalid argument: {args[0]}");
}
else
	Console.WriteLine("only one numeric arugment allowed.");