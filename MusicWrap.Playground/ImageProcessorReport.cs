#:property TargetFramework=net10.0-windows
#:project ../MusicWrap.Core/MusicWrap.Core.csproj
#:package Microsoft.Extensions.Logging.Console@10.*

using Microsoft.Extensions.Logging;
using MusicWrap.Core.Services.Images;

using var loggerFactory = LoggerFactory.Create(b => b
    .SetMinimumLevel(LogLevel.Debug)
    .AddSimpleConsole(o => o.SingleLine = true));

var extractor = new ImageProcessor(loggerFactory.CreateLogger<ImageProcessor>());

if (args.Length < 1)
{
    Console.Error.WriteLine("Usage: dotnet run -- <image-file>");
    return 1;
}

List<string> files = new List<string>();

for (int i = 0; i < args.Length; i++)
{
    files.Add(args[i]);
}

List<(string File, byte[] bytes, ImageProcessor.KmeansResult Result)> results = [];

foreach (var file in files)
{
    if (!File.Exists(file))
    {
        Console.Error.WriteLine($"File not found: {file}");
        continue;
    }
    Console.WriteLine($"Processing: {file}");

    var imageBytes = await File.ReadAllBytesAsync(file);
    var output = extractor.ExecuteKmeans(imageBytes);

    results.Add((Path.GetFileName(file), imageBytes, output));
}

Console.WriteLine($"Writing report to out/imageClusteringReport.html");

var html = $$"""
<html>
<body style="font-family:sans-serif;background:#222;color:#eee">
<div style="display:flex;flex-direction:column;gap:12px;padding:12px">
    {{string.Join("\n", results.Select(r => $$"""
        <div style="background:#333;padding:12px;border-radius:6px;display:flex; gap:24px;">
            <div>
                <h2>Original</h2>
                <h3>{{r.File}}</h3>
                <img src="data:image/png;base64,{{Convert.ToBase64String(r.bytes)}}" style="max-width:400px;max-height:400px">
            </div>
            <div style="display:flex;flex-direction:column;gap:6px">
                <h2>Colors</h2>
                <div style="display:flex;gap:6px">
                    <div style="width:24px;height:24px;background:{{r.Result.Result.DominantColorHex}}"></div>
                    <div>{{r.Result.Result.DominantColorHex}}</div>
                </div>
                <div style="display:flex;gap:6px">
                    <div style="width:24px;height:24px;background:{{r.Result.Result.DominantForegroundHex}}"></div>
                    <div>{{r.Result.Result.DominantForegroundHex}}</div>
                </div>
                <div style="display:flex;gap:6px">
                    <div style="width:24px;height:24px;background:{{r.Result.Result.HighlightColorHex}}"></div>
                    <div>{{r.Result.Result.HighlightColorHex}}</div>
                </div>
                <div style="display:flex;gap:6px">
                    <div style="width:24px;height:24px;background:{{r.Result.Result.HighlightForegroundHex}}"></div>
                    <div>{{r.Result.Result.HighlightForegroundHex}}</div>
                </div>
            </div>
            <div style="display:flex;flex-direction:column;gap:6px">
                <h2>Contrast Test</h2>
                
                <div style="min-width:20ch;aspect-ratio:1;padding:12px;background:{{r.Result.Result.DominantColorHex}}">
                    <div style="margin: auto;color:{{r.Result.Result.DominantForegroundHex}};">Dominant Color</div>
                </div>

                <div style="min-width:20ch;aspect-ratio:1;padding:12px;background:{{r.Result.Result.HighlightColorHex}}">
                    <div style="margin: auto;color:{{r.Result.Result.HighlightForegroundHex}};">Highlight Color</div>
                </div>
                
                <div style="min-width:20ch;aspect-ratio:1;padding:12px;background:{{r.Result.Result.DominantColorHex}}">
                    <div style="margin: auto;color:{{r.Result.Result.DominantForegroundHex}};">Dominant Color</div>
                    <div style="aspect-ratio:1;padding:12px;margin-top:8px;background:{{r.Result.Result.HighlightColorHex}}">
                        <div style="margin: auto;color:{{r.Result.Result.HighlightForegroundHex}};">Highlight Color</div>
                    </div>
                </div>
            </div>
            <div style="display:flex;flex-direction:column;gap:6px">
                <h2>Clusters</h2>
                {{string.Join("\n", r.Result.Clusters.Select(c => $$"""
                    <div style="display:flex;gap:6px">
                        <div style="width:24px;height:24px;background:rgb({{(int)c.Center.X}},{{(int)c.Center.Y}},{{(int)c.Center.Z}})"></div>
                        <div>Count: {{c.Count}}</div>
                    </div>
                    """))}}
            </div>
        </div>
        """
        ))}}
</div>
</body>
</html>
""";

File.WriteAllText("out/imageClusteringReport.html", html);
return 0;