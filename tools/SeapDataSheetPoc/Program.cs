using SeapDataSheetPoc;

string root = AppContext.BaseDirectory;
string corpusDir = Path.Combine(root, "corpus");
string mode = args.Length > 0 ? args[0] : "extract";

switch (mode)
{
    case "dump":
        return TextDumper.Run(corpusDir, Path.Combine(root, "dump"));
    case "extract":
    case "verify":
        return DataSheetRunner.Run(corpusDir, Path.Combine(root, "results"), verify: mode == "verify");
    default:
        Console.Error.WriteLine($"unknown mode {mode}");
        return 2;
}

