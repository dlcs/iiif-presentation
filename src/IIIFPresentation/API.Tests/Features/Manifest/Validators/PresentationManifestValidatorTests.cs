﻿using API.Features.Manifest.Validators;
using API.Settings;
using AWS.Settings;
using DLCS;
using FluentValidation.TestHelper;
using IIIF.Presentation.V3;
using Microsoft.Extensions.Options;
using Models.API.Manifest;

namespace API.Tests.Features.Manifest.Validators;

public class PresentationManifestValidatorTests
{
    private readonly PresentationManifestValidator sut = new(Options.Create(new ApiSettings()
    {
        AWS = new AWSSettings(),
        DLCS = new DlcsSettings
        {
            ApiUri = new Uri("https://localhost")
        }
    }));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Slug_Required(string? slug)
    {
        var manifest = new PresentationManifest { Slug = slug };
        
        var result = sut.TestValidate(manifest);
        result.ShouldHaveValidationErrorFor(m => m.Slug);
    }
    
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Parent_Required(string? parent)
    {
        var manifest = new PresentationManifest { Parent = parent };
        
        var result = sut.TestValidate(manifest);
        result.ShouldHaveValidationErrorFor(m => m.Parent);
    }
    
    [Fact]
    public void CanvasPaintingAndItems_Manifest_ErrorWhenDefaultSettings()
    {
        var manifest = new PresentationManifest
        {
            Items = new List<Canvas>()
            {
                new ()
                {
                    Id = "someId",
                }
            },
            PaintedResources = new List<PaintedResource>()
            {
                new ()
                {
                    CanvasPainting = new CanvasPainting()
                    {
                        CanvasId = "someCanvasId"
                    }
                }
            },
        };
        
        var result = sut.TestValidate(manifest);
        result.ShouldHaveValidationErrorFor(m => m.Items)
            .WithErrorMessage("The properties \"items\" and \"paintedResource\" cannot be used at the same time");
    }
    
    [Fact]
    public void CanvasPaintingAndItems_Manifest_NoErrorWhenSettings()
    {
        var sutAllowedItemsAndPaintedResource = new  PresentationManifestValidator(Options.Create(new ApiSettings()
        {
            AWS = new AWSSettings(),
            IgnorePaintedResourcesWithItems = true,
            DLCS = new DlcsSettings
            {
                ApiUri = new Uri("https://localhost")
            }
        }));
        
        var manifest = new PresentationManifest
        {
            Items = new List<Canvas>()
            {
                new ()
                {
                    Id = "someId",
                }
            },
            PaintedResources = new List<PaintedResource>()
            {
                new ()
                {
                    CanvasPainting = new CanvasPainting()
                    {
                        CanvasId = "someCanvasId"
                    }
                }
            },
        };
        
        var result = sutAllowedItemsAndPaintedResource.TestValidate(manifest);
        result.ShouldNotHaveValidationErrorFor(m => m.Items);
    }
    
    [Fact]
    public void PaintedResource_Manifest_ErrorWhenDuplicateChoiceWithCanvasOrder()
    {
        var manifest = new PresentationManifest
        {
            PaintedResources =
            [
                new PaintedResource
                {
                    CanvasPainting = new CanvasPainting
                    {
                        CanvasId = "someCanvasId-1",
                        CanvasOrder = 1,
                        ChoiceOrder = 1
                    }
                },

                new PaintedResource
                {
                    CanvasPainting = new CanvasPainting
                    {
                        CanvasId = "someCanvasId-2",
                        CanvasOrder = 1,
                        ChoiceOrder = 1
                    }
                }
            ],
        };
        
        var result = sut.TestValidate(manifest);
        result.ShouldHaveValidationErrorFor(m => m.PaintedResources)
            .WithErrorMessage("'choiceOrder' cannot be a duplicate within a 'canvasOrder'");
    }
    
    [Fact]
    public void PaintedResource_Manifest_NoErrorWhenNoDuplicateChoiceWithCanvasOrder()
    {
        var manifest = new PresentationManifest
        {
            PaintedResources =
            [
                new PaintedResource
                {
                    CanvasPainting = new CanvasPainting
                    {
                        CanvasId = "someCanvasId-1",
                        CanvasOrder = 1,
                        ChoiceOrder = 1
                    }
                },

                new PaintedResource
                {
                    CanvasPainting = new CanvasPainting
                    {
                        CanvasId = "someCanvasId-2",
                        CanvasOrder = 2,
                        ChoiceOrder = 1
                    }
                }
            ],
        };
        
        var result = sut.TestValidate(manifest);
        result.ShouldNotHaveValidationErrorFor(m => m.PaintedResources);
    }
    
    [Fact]
    public void PaintedResource_Manifest_ErrorWhenNoChoiceWithCanvasOrder()
    {
        var manifest = new PresentationManifest
        {
            Slug = "someSlug",
            Parent = "https://someParent",
            PaintedResources =
            [
                new PaintedResource
                {
                    CanvasPainting = new CanvasPainting
                    {
                        CanvasId = "someCanvasId-1",
                        CanvasOrder = 1
                    }
                },

                new PaintedResource
                {
                    CanvasPainting = new CanvasPainting
                    {
                        CanvasId = "someCanvasId-2",
                        CanvasOrder = 1
                    }
                },
                
                new PaintedResource
                {
                    CanvasPainting = new CanvasPainting
                    {
                        CanvasId = "someCanvasId-3",
                        CanvasOrder = 1,
                        ChoiceOrder = 1
                    }
                }
            ],
        };
        
        var result = sut.TestValidate(manifest);
        result.ShouldHaveValidationErrorFor(m => m.PaintedResources)
            .WithErrorMessage("'choiceOrder' cannot be null within a duplicate 'canvasOrder'");
        
        result.Errors.Should().HaveCount(1);
    }
    
    [Fact]
    public void CanvasPaintingAndItems_Manifest_NoErrorWhenNoChoiceWithIndividualCanvasOrder()
    {
        var manifest = new PresentationManifest
        {
            PaintedResources =
            [
                new PaintedResource
                {
                    CanvasPainting = new CanvasPainting()
                    {
                        CanvasId = "someCanvasId-1",
                        CanvasOrder = 1,
                        ChoiceOrder = 1
                    }
                },

                new PaintedResource
                {
                    CanvasPainting = new CanvasPainting()
                    {
                        CanvasId = "someCanvasId-2",
                        CanvasOrder = 2
                    }
                }
            ],
        };
        
        var result = sut.TestValidate(manifest);
        result.ShouldNotHaveValidationErrorFor(m => m.PaintedResources);
    }

    [Fact]
    public void CanvasPainting_WithStaticSize_NoErrorWhenBothValues()
    {
        var manifest = new PresentationManifest
        {
            PaintedResources =
            [
                new PaintedResource
                {
                    CanvasPainting = new()
                    {
                        CanvasId = "someCanvasId-2137",
                        StaticWidth = 100,
                        StaticHeight = 100
                    }
                }
            ]
        };

        var result = sut.TestValidate(manifest);
        result.ShouldNotHaveValidationErrorFor(m => m.PaintedResources);
    }

    [Fact]
    public void CanvasPainting_WithStaticSize_ErrorWhenOneMissing()
    {
        var manifest = new PresentationManifest
        {
            PaintedResources =
            [
                new PaintedResource
                {
                    CanvasPainting = new()
                    {
                        CanvasId = "someCanvasId-2137",
                        StaticWidth = 100
                    }
                }
            ]
        };

        var result = sut.TestValidate(manifest);
        result.ShouldHaveValidationErrorFor(m => m.PaintedResources);
    }
    
    [Fact]
    public void CanvasPaintingAndItems_Manifest_NoErrorWhenNoChoiceNoCanvasMultiple()
    {
        var manifest = new PresentationManifest
        {
            PaintedResources =
            [
                new PaintedResource
                {
                    CanvasPainting = new CanvasPainting()
                    {
                        CanvasId = "someCanvasId-1"
                    }
                },

                new PaintedResource
                {
                    CanvasPainting = new CanvasPainting()
                    {
                        CanvasId = "someCanvasId-2",
                    }
                }
            ],
        };
        
        var result = sut.TestValidate(manifest);
        result.ShouldNotHaveValidationErrorFor(m => m.PaintedResources);
    }
    
    [Fact]
    public void PaintedResource_Manifest_NoErrorWhenChoiceOfOne()
    {
        var manifest = new PresentationManifest
        {
            PaintedResources =
            [
                new PaintedResource
                {
                    CanvasPainting = new CanvasPainting
                    {
                        CanvasId = "someCanvasId-1",
                        CanvasOrder = 1,
                        ChoiceOrder = 1
                    }
                },
                new PaintedResource
                {
                    CanvasPainting = new CanvasPainting
                    {
                        CanvasId = "someCanvasId-2",
                        CanvasOrder = 2,
                        ChoiceOrder = 1
                    }
                }
            ],
        };
        
        var result = sut.TestValidate(manifest);
        result.ShouldNotHaveValidationErrorFor(m => m.PaintedResources);
    }
    
    [Fact]
    public void CanvasPaintingAndItems_Manifest_ErrorWhenNotEveryItemHasCanvasOrder()
    {
        var manifest = new PresentationManifest
        {
            PaintedResources =
            [
                new PaintedResource
                {
                    CanvasPainting = new CanvasPainting()
                    {
                        CanvasId = "someCanvasId-1",
                        CanvasOrder = 1
                    }
                },

                new PaintedResource
                {
                    CanvasPainting = new CanvasPainting()
                    {
                        CanvasId = "someCanvasId-2",
                    }
                }
            ],
        };
        
        var result = sut.TestValidate(manifest);
        result.ShouldHaveValidationErrorFor(m => m.PaintedResources)
            .WithErrorMessage("'canvasOrder' is required on all resources when used in at least one");
    }
    
    [Fact]
    public void CanvasPaintingAndItems_Manifest_ErrorWhenNotEveryItemHasCanvasId()
    {
        var manifest = new PresentationManifest
        {
            PaintedResources =
            [
                new PaintedResource
                {
                    CanvasPainting = new CanvasPainting
                    {
                        CanvasId = "someCanvasId"
                    }
                },

                new PaintedResource
                {
                    CanvasPainting = new CanvasPainting
                    {
                        CanvasId = null
                    }
                }
            ],
        };
        
        var result = sut.TestValidate(manifest);
        result.ShouldHaveValidationErrorFor(m => m.PaintedResources)
            .WithErrorMessage("'canvasId' is required on all resources when used in at least one");
    }
}
