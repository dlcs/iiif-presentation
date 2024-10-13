
using Demo;
using IIIF.Presentation.V3;
using IIIF.Presentation.V3.Strings;
using IIIF.Serialisation;
using Mapper;
using Mapper.DlcsApi;
using System.Text;
using System.Text.Json;

if (args.Length == 0)
{
    Console.WriteLine("Supply Manifest file path or URL as arg");
    return;
}

var sb = new StringBuilder();

if (args[0] == "cookbook")
{
    sb.AppendAndWriteLine("# Cookbook recipes");
    sb.AppendAndWriteLine();

    var httpClient = new HttpClient();
    var theseusColl = await httpClient.GetStringAsync("https://theseus-viewer.netlify.app/cookbook-collection.json");
    var coll = theseusColl.FromJson<Collection>();
    var skip = new List<string>
        {
            "https://iiif.io/api/cookbook/recipe/0219-using-caption-file/manifest.json"
        };

    AddSomeExtrasToCookbook(coll);
    foreach (var item in coll.Items ?? [])
    {
        if(item is Manifest manifest)
        {
            if(skip.Contains(manifest.Id!))
            { 
                continue; 
            }
            sb.AppendAndWriteLine();
            sb.AppendAndWriteLine("## " + manifest.Label!["en"][0].TrimStart('✅').TrimStart(' '));
            sb.AppendAndWriteLine(manifest.Id);
            sb.AppendAndWriteLine();
            var s = await httpClient.GetStringAsync(manifest.Id);
            ParseManifest(s, sb);
            sb.AppendAndWriteLine();
        }
    }
    File.WriteAllText("..\\.\\..\\..\\output.md", sb.ToString());
    return;
}


if (args[0].StartsWith("http"))
{
    var httpClient = new HttpClient();
    sb.AppendAndWriteLine("Fetching Manifest JSON from " + args[0]);
    var s = await httpClient.GetStringAsync(args[0]);
    ParseManifest(s, sb);
}
else
{
    sb.AppendAndWriteLine("Loading Manifest JSON from " + args[0]);
    var s = File.ReadAllText(args[0]);
    ParseManifest(s, sb);
}


static void ParseManifest(string manifestJson, StringBuilder sb)
{
    var parser = new Parser();
    var manifest = manifestJson.FromJson<Manifest>();
    TweakForTesting(manifest);
    var entities = parser.ParseManifest(manifest);
    sb.AppendAndWriteLine();
    sb.AppendAndWriteLine("### canvas_painting rows");
    sb.AppendAndWriteLine();
    sb.AppendAndWriteLine(entities.ToMarkdownTable());
    sb.AppendAndWriteLine();
    sb.AppendAndWriteLine();

    var pseudoManifest = new PseudoManifest
    {
        Id = "https://dlc.services/iiif/99/manifests/" + entities[0].ManifestId,
        PaintedResources = parser.GetPaintedResources(entities)
    };
    var json = JsonSerializer.Serialize(pseudoManifest,
        new JsonSerializerOptions()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        });
    sb.AppendAndWriteLine("### paintedResources property in DLCS Manifest");
    sb.AppendAndWriteLine();
    sb.AppendAndWriteLine("```json");
    sb.AppendAndWriteLine(json);
    sb.AppendAndWriteLine("```");
    sb.AppendAndWriteLine();
    sb.AppendAndWriteLine();
}

static void TweakForTesting(Manifest? manifest)
{
    if (manifest!.Id == "https://iiif.io/api/cookbook/recipe/0434-choice-av/manifest.json")
    {
        // add a label to the Canvas to test CanvasLabel
        manifest.Items![0].Label = LangMap("Pick one of these formats");
    }
    if(manifest!.Id == "https://iiif.io/api/cookbook/recipe/0040-image-rotation-service/manifest-service.json")
    {
        manifest.Id = manifest.Id.TrimEnd();
    }
}


void AddSomeExtrasToCookbook(Collection coll)
{
    // coll.Items!.Add(new Manifest { Id = "https://iiif.wellcomecollection.org/presentation/b18035723", Label = LangMap("Wunder external") });
    coll.Items!.Add(new Manifest { Id = "https://dlcs.io/iiif-resource/wellcome/preview/5/b18035723", Label = LangMap("Wunder internal") });
}

static LanguageMap LangMap(string s)
{
    return new LanguageMap("en", s);
}