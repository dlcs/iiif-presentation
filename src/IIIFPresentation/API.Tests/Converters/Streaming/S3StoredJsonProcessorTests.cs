using System.Text;
using API.Converters.Streaming;
using API.Tests.Helpers;
using LateApexEarlySpeed.Xunit.Assertion.Json;
using Models.Database.General;
using Repository.Paths;

namespace API.Tests.Converters.Streaming;

public class S3StoredJsonProcessorTests
{
    private readonly IPathGenerator pathGenerator = TestPathGenerator.CreatePathGenerator("localhost", Uri.UriSchemeHttp);

    [Fact]
    public void ProcessJson_ChangesTopLevelId()
    {
        const string requestSlug =
            "0004_hierarchical_iiif_collection_2O71nF/0004_hierarchical_iiif_collection_child_PWZnry";
        const int customerId = 52;

        var result = GetProcessed(ManifestJsonWithId,
            new(pathGenerator.GenerateHierarchicalId(new Hierarchy
                { Slug = requestSlug, CustomerId = customerId, FullPath = requestSlug })));

        JsonAssertion.Meet(root => root.IsJsonObject()
                .HasProperty("id", p => p.IsJsonString().Equal(
                    $"http://localhost/52/{requestSlug}")),
            result);
    }

    [Fact]
    public void ProcessJson_AddsTopLevelId()
    {
        const string requestSlug =
            "0004_hierarchical_iiif_collection_2O71nF/0004_hierarchical_iiif_collection_child_PWZnry";
        const int customerId = 52;

        var result = GetProcessed(ManifestJsonWithoutId, new(
            pathGenerator.GenerateHierarchicalId(new Hierarchy
                { Slug = requestSlug, CustomerId = customerId, FullPath = requestSlug })));

        JsonAssertion.Meet(root => root.IsJsonObject()
                .HasProperty("id", p => p.IsJsonString().Equal(
                    $"http://localhost/52/{requestSlug}")),
            result);
    }

    private static string GetProcessed(string input, S3StoredJsonProcessor processor)
    {
        using var inputStream = new MemoryStream(Encoding.UTF8.GetBytes(input));
        using var outputStream = new MemoryStream();
        StreamingJsonProcessor.ProcessJson(inputStream, outputStream, inputStream.Length, processor);
        return Encoding.UTF8.GetString(outputStream.ToArray());
    }

    private const string ManifestJsonWithId =
        """
        {
          "id": "https://example.digirati.io/presentation/item/582382",
          "type": "Manifest",
          "label": {
            "en": [
              "Quirk Fnord Pitter Patter 2045-23-99"
            ]
          },
          "thumbnail": [
            {
              "id": "https://example.digirati.io/thumbs/5/8/552332_1_0420/full/155,200/0/default.jpg",
              "type": "Image",
              "format": "image/jpeg",
              "service": [
                {
                  "@context": "http://example.com/api/image/2/context.json",
                  "@id": "https://example.digirati.io/thumbs/v2/4/7/542382_0_0420",
                  "@type": "ImageService2",
                  "profile": "http://example.com/api/image/2/level0.json",
                  "sizes": [
                    {"width":791,"height":1024},
                    {"width":309,"height":400},
                    {"width":155,"height":200},
                    {"width":77,"height":100}
                  ]
                },
                {
                  "@context": "http://example.com/api/image/3/context.json",
                  "id": "https://example.digirati.io/thumbs/6/8/54322_0_0000",
                  "type": "ImageService3",
                  "profile": "level0",
                  "sizes": [
                    {"width":791,"height":1024},
                    {"width":309,"height":400},
                    {"width":155,"height":200},
                    {"width":77,"height":100}
                  ]
                }
              ]
            }
          ],
          "items": []
        }
        """;

    private const string ManifestJsonWithoutId =
        """
        {
          "type": "Manifest",
          "label": {
            "en": [
              "Quirk Fnord Pitter Patter 2045-23-99"
            ]
          },
          "thumbnail": [
            {
              "id": "https://example.digirati.io/thumbs/5/8/552332_1_0420/full/155,200/0/default.jpg",
              "type": "Image",
              "format": "image/jpeg",
              "service": [
                {
                  "@context": "http://example.com/api/image/2/context.json",
                  "@id": "https://example.digirati.io/thumbs/v2/4/7/542382_0_0420",
                  "@type": "ImageService2",
                  "profile": "http://example.com/api/image/2/level0.json",
                  "sizes": [
                    {"width":791,"height":1024},
                    {"width":309,"height":400},
                    {"width":155,"height":200},
                    {"width":77,"height":100}
                  ]
                },
                {
                  "@context": "http://example.com/api/image/3/context.json",
                  "id": "https://example.digirati.io/thumbs/6/8/54322_0_0000",
                  "type": "ImageService3",
                  "profile": "level0",
                  "sizes": [
                    {"width":791,"height":1024},
                    {"width":309,"height":400},
                    {"width":155,"height":200},
                    {"width":77,"height":100}
                  ]
                }
              ]
            }
          ],
          "items": []
        }
        """;
}
