using API.Converters;

namespace API.Tests.Converters;

public class FastJsonPropertyReadTests
{
    [Fact]
    public void FindAtLevel_FindsValue_DefaultTopLevel()
    {
        FastJsonPropertyRead.FindAtLevel(Json, "searchedProperty").Should()
            .Be("fnord", "it's the value of 'searchedProperty' in sample JSON");
    }

    [Fact]
    public void FindAtLevel_FindsValue_Deeper()
    {
        FastJsonPropertyRead.FindAtLevel(Json, "priority", 3).Should()
            .Be("high", "it's the value of first instance of 'priority' property in sample JSON");
    }
    
    [Fact]
    public void FindAtLevel_ReturnsNull_IfNotFound()
    {
      FastJsonPropertyRead.FindAtLevel(Json, "i don't exist in the json").Should()
        .BeNull("no such property in the JSON");
    }

    // just some arbitrary JSON - the method under test is not IIIF specific
    private const string Json =

        #region <sample json>

        """
        {
          "id": 42,
          "name": "Sample Root",
          "enabled": true,
          "tags": ["alpha", "beta", "gamma"],
          "metadata": {
            "created": "2025-02-15T12:34:56Z",
            "updated": "2025-03-01T08:12:45Z",
            "attributes": {
              "priority": "high",
              "flags": {
                "archived": false,
                "requiresReview": true,
                "internal": {
                  "level": 3,
                  "notes": ["check format", "verify fields"]
                }
              }
            }
          },
          "items": [
            {
              "id": "a1",
              "quantity": 10,
              "details": {
                "manufacturer": "Acme Corp",
                "dimensions": {
                  "width": 10.5,
                  "height": 20.0,
                  "depth": 5.75
                }
              }
            },
            {
              "id": "b2",
              "quantity": 4,
              "details": {
                "manufacturer": "Globex",
                "dimensions": {
                  "width": 5.0,
                  "height": 8.0,
                  "depth": 1.25,
                  "history": [
                    {
                      "revision": 1,
                      "notes": { "editor": "system", "timestamp": "2025-01-01T00:00:00Z" }
                    },
                    {
                      "revision": 2,
                      "notes": { "editor": "qa", "timestamp": "2025-02-01T00:00:00Z" }
                    }
                  ]
                }
              }
            }
          ],
          "config": {
            "mode": "full",
            "retry": 3,
            "options": {
              "validate": true,
              "paths": [
                { "name": "input", "value": "/var/data/in" },
                { "name": "output", "value": "/var/data/out" }
              ]
            }
          },
          "searchedProperty": "fnord"
        }
        """;

    #endregion
}
