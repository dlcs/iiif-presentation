
using IIIF.Presentation.V3;
using IIIF.Serialisation;
using Mapper;
using System.Text.Json;

if (args.Length == 0)
{
    Console.WriteLine("Supply Manifest file path or URL as arg");
    return;
}

if (args[0] == "cookbook")
{
    var httpClient = new HttpClient();
    var theseusColl = await httpClient.GetStringAsync("https://theseus-viewer.netlify.app/cookbook-collection.json");
    var coll = theseusColl.FromJson<Collection>();
    var skip = new List<string>
        {
            "https://iiif.io/api/cookbook/recipe/0219-using-caption-file/manifest.json",
            "https://iiif.io/api/cookbook/recipe/0040-image-rotation-service/manifest-service.json"
        };
    foreach (var item in coll.Items ?? [])
    {
        if(item is Manifest manifest)
        {
            if(skip.Contains(manifest.Id))
            { 
                continue; 
            }
            Console.WriteLine();
            Console.WriteLine("COOKBOOK RECIPE: " + manifest.Label!["en"][0]);
            Console.WriteLine(manifest.Id);
            Console.WriteLine();
            var s = await httpClient.GetStringAsync(manifest.Id);
            ParseManifest(s);
            Console.WriteLine();
        }
    }
    return;
}

if (args[0].StartsWith("http"))
{
    var httpClient = new HttpClient();
    Console.WriteLine("Fetching Manifest JSON from " + args[0]);
    var s = await httpClient.GetStringAsync(args[0]);
    ParseManifest(s);
}
else
{
    Console.WriteLine("Loading Manifest JSON from " + args[0]);
    var s = File.ReadAllText(args[0]);
    ParseManifest(s);
}

static void ParseManifest(string manifestJson)
{
    var parser = new Parser();
    var manifest = manifestJson.FromJson<Manifest>();
    var entities = parser.ParseManifest(manifest);
    Console.WriteLine();
    Console.WriteLine("===== canvas_painting rows =====");
    Console.WriteLine();
    Console.WriteLine(entities.ToMarkdownTable());
    Console.WriteLine();
    Console.WriteLine();

    var paintedResources = parser.GetPaintedResources(entities);
    var json = JsonSerializer.Serialize(paintedResources,
        new JsonSerializerOptions()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        });
    Console.WriteLine("===== paintedResources property in DLCS Manifest =======");
    Console.WriteLine(json);
    Console.WriteLine();
    Console.WriteLine();
}

