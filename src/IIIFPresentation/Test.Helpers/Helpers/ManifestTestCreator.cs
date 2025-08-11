using IIIF.ImageApi.V2;
using IIIF.ImageApi.V3;
using IIIF.Presentation.V3;
using IIIF.Presentation.V3.Annotation;
using IIIF.Presentation.V3.Content;
using IIIF.Presentation.V3.Strings;
using Models.Database;
using Models.DLCS;

namespace Test.Helpers.Helpers;

public static class ManifestTestCreatorX
{
    public static Manifest Build(this GenerateManifestOptions options) => ManifestTestCreator.GenerateManifest(options);

    public static GenerateManifestOptions WithCanvas(this GenerateManifestOptions options,
        Action<GenerateCanvasOptions> configure) => options.WithCanvas((string?)null, configure);

    public static GenerateManifestOptions WithCanvas(this GenerateManifestOptions options,
        string? id, Action<GenerateCanvasOptions> configure)
    {
        options.Canvases ??= [];
        var canvas = new GenerateCanvasOptions { Id = id };
        configure(canvas);
        options.Canvases.Add(canvas);
        return options;
    }

    public static GenerateManifestOptions WithCanvas(this GenerateManifestOptions options,
        AssetId? id, Action<GenerateCanvasOptions> configure)
        // Canvas.Id is parsed to find the asset-id so needs to be in set format
        => options.WithCanvas($"https://dlcs.test/iiif-img/{id}/canvas/c/", configure);

    public static GenerateCanvasOptions With(this GenerateCanvasOptions options, GenerateCanvasOptions.Content type) =>
        options.WithMultiple(type);

    public static GenerateCanvasOptions WithMultiple(this GenerateCanvasOptions options,
        GenerateCanvasOptions.Content type, int count = 1)
    {
        options.ContentType = type;
        options.Count = count;
        return options;
    }

    public static GenerateCanvasOptions ForceChoiceUse(this GenerateCanvasOptions options, bool value = true)
    {
        options.PaintingChoiceOverride = value;
        return options;
    }

    public static GenerateCanvasOptions WithImage(this GenerateCanvasOptions options)
        => options.With(GenerateCanvasOptions.Content.Image);

    public static GenerateCanvasOptions WithImages(this GenerateCanvasOptions options, int count = 1)
        => options.WithMultiple(GenerateCanvasOptions.Content.Image, count);

    public static GenerateCanvasOptions WithSound(this GenerateCanvasOptions options)
        => options.With(GenerateCanvasOptions.Content.Sound);

    public static GenerateCanvasOptions WithSounds(this GenerateCanvasOptions options, int count = 1)
        => options.WithMultiple(GenerateCanvasOptions.Content.Sound, count);

    public static GenerateCanvasOptions WithVideo(this GenerateCanvasOptions options)
        => options.With(GenerateCanvasOptions.Content.Video);

    public static GenerateCanvasOptions WithVideos(this GenerateCanvasOptions options, int count = 1)
        => options.WithMultiple(GenerateCanvasOptions.Content.Video, count);

    public static Canvas Build(this GenerateCanvasOptions options)
        => ManifestTestCreator.GenerateCanvas(options);

    public static List<CanvasPainting> Build(this GenerateCanvasPaintingsOptions options) => 
        ManifestTestCreator.GenerateCanvasPaintings(options);
    
    public static GenerateCanvasPaintingOptions WithCanvasChoiceOrder(this GenerateCanvasPaintingOptions options, 
        int canvasOrder, int choiceOrder)
    {
        options.Id = options.Id;
        options.CanvasOrder = canvasOrder;
        options.ChoiceOrder = choiceOrder;
        
        return options;
    }
    
    public static GenerateCanvasPaintingsOptions WithCanvasPainting(this GenerateCanvasPaintingsOptions options,
        string id, Action<GenerateCanvasPaintingOptions> configure)
    {
        options.GenerateCanvasPaintingOptions ??= [];
        var canvasPainting = new GenerateCanvasPaintingOptions { Id = id };
        configure(canvasPainting);
        options.GenerateCanvasPaintingOptions.Add(canvasPainting);
        return options;
    }
}

public class GenerateCanvasPaintingOptions
{
    public int CanvasOrder { get; set; }
    public int ChoiceOrder { get; set; }
    public string? Id { get; set; }
    public string? AssetId { get; set; }
    public Uri? CanvasOriginalId { get; set; }
    public LanguageMap? CanvasLabel { get; set; }
    public LanguageMap? Label { get; set; }
}

public class GenerateCanvasPaintingsOptions
{
    public required string Id { get; set; }
    
    public List<GenerateCanvasPaintingOptions>? GenerateCanvasPaintingOptions { get; set; }
}

public class GenerateCanvasOptions
{
    public enum Content
    {
        Empty,
        Image,
        Sound,
        Video,
        Dataset
    }

    public string? Id { get; set; }
    public bool? PaintingChoiceOverride { get; set; }
    public bool UsePaintingChoice => PaintingChoiceOverride ?? Count > 1;
    public int Count { get; set; } = 1;
    public Content ContentType { get; set; }
}

public class GenerateManifestOptions
{
    public string? Id { get; set; }
    public List<GenerateCanvasOptions>? Canvases { get; set; }
}

public class ManifestTestCreator
{
    public static GenerateManifestOptions New(string? id = null) => new() {Id = id};
    public static GenerateCanvasOptions Canvas(string? id = null) => new() {Id = id};
    
    public static GenerateCanvasPaintingsOptions CanvasPaintings(string? id = null) => new() {Id = id};

    public static List<CanvasPainting> GenerateCanvasPaintings(GenerateCanvasPaintingsOptions options)
    {
        var canvasOrder = 0;
        return options.GenerateCanvasPaintingOptions.Select(cp => new CanvasPainting
        {
            Id = cp.Id, 
            AssetId = cp.AssetId != null ? AssetId.FromString(cp.AssetId) : null, 
            CanvasOriginalId = cp.CanvasOriginalId != null ? cp.CanvasOriginalId : null,
            CanvasOrder = cp.CanvasOrder,
            ChoiceOrder = cp.ChoiceOrder,
            Label = cp.Label,
            CanvasLabel = cp.CanvasLabel
        }).ToList();
    }
    
    public static List<CanvasPainting> GenerateCanvasPaintings(List<string> idList)
    {
        var canvasOrder = 0;
        return idList.Select(id => new CanvasPainting
        {
            Id = id, AssetId = AssetId.FromString(id), CanvasOrder = canvasOrder++,
            Label = new("canvasPaintingLabel", "generated canvas painting label")
        }).ToList();
    }
    
    public static List<CanvasPainting> GenerateCanvasPaintings(params AssetId[] idList)
    {
        var canvasOrder = 0;
        return idList.Select(id => new CanvasPainting
        {
            Id = id + $"_{Guid.NewGuid()}", AssetId = id, CanvasOrder = canvasOrder++, Ingesting = true,
            Label = new("canvasPaintingLabel", "generated canvas painting label")
        }).ToList();
    }
    
    public static List<CanvasPainting> GenerateCanvasPaintings(params Uri[] canvasOriginalIdList)
    {
        var canvasOrder = 0;
        return canvasOriginalIdList.Select(id => new CanvasPainting
        {
            Id = id + $"_{Guid.NewGuid()}", CanvasOriginalId = id, CanvasOrder = canvasOrder++, Ingesting = true,
            Label = new("canvasPaintingLabel", "generated canvas painting label")
        }).ToList();
    }

    public static Manifest GenerateManifest(GenerateManifestOptions options)
    {
        return new()
        {
            Thumbnail =
            [
                GenerateImageService(options.Id ?? options.Canvases?.FirstOrDefault()?.Id)
            ],
            Label = new("en", "someLabel"),
            Items = options.Canvases?.Select(c =>
            {
                c.Id ??= options.Id;
                return GenerateCanvas(c);
            }).ToList(),
            Metadata = GenerateMetadata()
        };
    }

    public static Canvas GenerateCanvas(GenerateCanvasOptions options)
    {
        var id = options.Id;
        var temporal = options.ContentType is GenerateCanvasOptions.Content.Sound or GenerateCanvasOptions.Content.Video;
        var spatial = options.ContentType is GenerateCanvasOptions.Content.Image or GenerateCanvasOptions.Content.Video;
        return new()
        {
            Id = id,
            Label = new("en", $"{id}"),
            Duration = temporal ? 15000 : null,
            Width = spatial ? 110 : null,
            Height = spatial ? 110 : null,
            Metadata = GenerateMetadata(),
            Rendering = GenerateAnnotationRendering(options),
            Thumbnail =
            [
                new Image
                {
                    Id = $"{id}_CanvasThumbnail",
                    Width = 50,
                    Height = 50
                }
            ],
            Items =
            [
                new AnnotationPage
                {
                    Id = $"{id}/page",
                    Label = new("en", $"{id}_AnnotationPage"),
                    Items =
                    [
                        new PaintingAnnotation
                        {
                            Id = $"{id}/page/image",
                            Label = new("en", $"PaintingAnnotation_{id}_PaintingAnnotation"),
                            Body = GenerateAnnotationBody(options),
                            Service = new()
                            {
                                new ImageService3
                                {
                                    Id = $"{id}_ImageService3",
                                    Label = new("en", $"{id}_ImageService3"),
                                    Profile = "level2"
                                }
                            }
                        }
                    ]
                }
            ]
        };
    }

    private static List<ExternalResource>? GenerateAnnotationRendering(GenerateCanvasOptions options)
        => options.ContentType switch
        {
            GenerateCanvasOptions.Content.Dataset =>
            [
                new ExternalResource("Dataset")
                {
                    Label = new("en", $"{options.Id}_Dataset"),
                    Format = "application/pdf",
                    Behavior = ["original"]
                }
            ],
            GenerateCanvasOptions.Content.Sound =>
            [
                new ExternalResource("Sound")
                {
                    Label = new("en", $"{options.Id}_Audio File"),
                    Format = "audio/wav",
                    Behavior = ["original"]
                }
            ],
            GenerateCanvasOptions.Content.Video =>
            [
                new ExternalResource("Video")
                {
                    Label = new("en", $"{options.Id}_Video File"),
                    Format = "video/mp4",
                    Behavior = ["original"]
                }
            ],
            GenerateCanvasOptions.Content.Image =>
            [
                new ExternalResource("Image")
                {
                    Label = new("en", $"{options.Id}_Image File"),
                    Format = "image/jpeg",
                    Behavior = ["original"]
                }
            ],
            _ => null
        };

    private static IPaintable? GenerateAnnotationBody(GenerateCanvasOptions options)
        => options.UsePaintingChoice ? GeneratePaintingChoice(options) : GenerateResourceBody(options);

    private static IPaintable? GenerateResourceBody(GenerateCanvasOptions options)
    {
        return options.ContentType switch
        {
            GenerateCanvasOptions.Content.Empty => null,
            GenerateCanvasOptions.Content.Image => GenerateImage(options),
            GenerateCanvasOptions.Content.Sound => GenerateSound(options),
            GenerateCanvasOptions.Content.Video => GenerateVideo(options),
            GenerateCanvasOptions.Content.Dataset => GenerateImage(options),
            _ => throw new ArgumentOutOfRangeException(nameof(options))
        };
    }

    private static Sound GenerateSound(GenerateCanvasOptions options) =>
        new Sound
        {
            Id = options.Id,
            Duration = 15000,
            Format = "audio/mp3"
        };

    private static Video GenerateVideo(GenerateCanvasOptions options)
        => new Video
        {
            Id = options.Id,
            Duration = 16000,
            Width = 640,
            Height = 480,
            Format = "video/webm"
        };

    private static PaintingChoice GeneratePaintingChoice(GenerateCanvasOptions options)
    {
        return new()
        {
            Items = Enumerable.Range(1, options.Count).Select(i => GenerateResourceBody(options)!)
                .ToList()
        };
    }

    private static Image GenerateImage(GenerateCanvasOptions options) =>
        new()
        {
            Id = options.Id.Replace("/canvas/c/", "/full/100,100/0/default.jpg"),
            Width = 100,
            Height = 100
        };

    private static List<LabelValuePair> GenerateMetadata() =>
        [new(new("en", "label1"), new LanguageMap("en", "value1"))];

    public static Image GenerateImageService(string id) =>
        new()
        {
            Service =
            [
                new ImageService3
                {
                    Id = id,
                    Sizes = [new(100, 100)]
                }
            ],
            Label = new("en", $"{id}_Image")
        };
    
    public static Manifest GenerateMinimalNamedQueryManifest(AssetId fullAssetId, Uri presentationUrl, string? bodyId = null)
    {
        var qualifiedAssetId = $"{presentationUrl}iiif-img/{fullAssetId}";
        var canvasId = $"{qualifiedAssetId}/canvas/c/1";
        return new Manifest
        {
            Items =
            [
                new()
                {
                    Id = canvasId,
                    Width = 100,
                    Height = 100,
                    Thumbnail =
                    [
                        new Image
                        {
                            Id = "https://this-does-not-matter",
                            Service =
                            [
                                new ImageService2
                                {
                                    Profile = ImageService2.Level0Profile,
                                },

                                new ImageService3
                                {
                                    Profile = ImageService3.Level0Profile,
                                }
                            ]
                        }
                    ],
                    Metadata = [new("en", "Reference1", "foo")],
                    Items =
                    [
                        new()
                        {
                            Id = $"{canvasId}/page",
                            Items =
                            [
                                new PaintingAnnotation
                                {
                                    Id = $"{canvasId}/page/image",
                                    Body = new Image
                                    {
                                        Id = bodyId ?? $"{qualifiedAssetId}/full/100,100/0/default.jpg",
                                        Width = 100,
                                        Height = 100,
                                        Format = "image/jpeg",
                                        Service =
                                        [
                                            new ImageService2
                                            {
                                                Width = 75,
                                                Height = 75,
                                                Profile = ImageService2.Level2Profile
                                            },
                                            new ImageService3
                                            {
                                                Width = 75,
                                                Height = 75,
                                                Profile = ImageService3.Level2Profile
                                            }
                                        ]
                                    },
                                    Target = new Canvas { Id = canvasId }
                                }
                            ]
                        }
                    ]
                }
            ]
        };
    }
}
