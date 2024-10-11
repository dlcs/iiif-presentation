
using IIIF.Presentation.V3;
using IIIF.Serialisation;
using Mapper;
using System.Text.Json;

if (args.Length == 0)
{
    Console.WriteLine("Supply Manifest file path or URL as arg");
    return;
}

string? manifestJson;
var manifestAddr = args[0];
if (manifestAddr.StartsWith("http"))
{
    var httpClient = new HttpClient();
    Console.WriteLine("Fetching Manifest JSON from " + manifestAddr);
    manifestJson = await httpClient.GetStringAsync(manifestAddr);
}
else
{
    Console.WriteLine("Loading Manifest JSON from " + manifestAddr);
    manifestJson = File.ReadAllText(manifestAddr);
}

var manifest = manifestJson.FromJson<Manifest>();
var parser = new Parser();
var entities = parser.ParseManifest(manifest);
Console.WriteLine();
Console.WriteLine("===== canvas_painting rows =====");
Console.WriteLine(entities.ToMarkdownTable());
Console.WriteLine();
Console.WriteLine();

var paintedResources = parser.GetPaintedResources(entities);
var json = JsonSerializer.Serialize(paintedResources, 
    new JsonSerializerOptions(){
        WriteIndented=true, 
        PropertyNamingPolicy=JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    });
Console.WriteLine("===== paintedResources property in DLCS Manifest =======");
Console.WriteLine(json);





