using DLCS;
using IIIF.Presentation.V3;
using IIIF.Presentation.V3.Annotation;
using IIIF.Presentation.V3.Content;
using IIIF.Presentation.V3.Strings;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Models.DLCS;
using Repository.Paths;
using Services.Manifests;
using Services.Manifests.Helpers;
using Services.Manifests.Settings;
using Test.Helpers;
using Test.Helpers.Helpers;
using Canvas = IIIF.Presentation.V3.Canvas;
using Manifest = IIIF.Presentation.V3.Manifest;

namespace Services.Tests.Manifests.Helpers;

public class ManifestMergerManifestAdjunctTests
{
    private readonly IManifestMerger sut;
    private const int CustomerId = 1;
    private const string ManifestId = "test-manifest";
    private static readonly AssetId StubAssetId = new(CustomerId, ResourceAdjunctInteractions.StubAssetSpace, $"Manifest_{ManifestId}");

    public ManifestMergerManifestAdjunctTests()
    {
        var settingsBasedPathGenerator = new SettingsBasedPathGenerator(Options.Create(new DlcsSettings
        {
            ApiUri = new Uri("https://dlcs.api")
        }), new SettingsDrivenPresentationConfigGenerator(Options.Create(new PathSettings
        {
            PresentationApiUrl = new Uri("https://localhost:5000"),
            PathRules = PathRewriteOptions.Default
        })));

        var pathRewriteParser =
            new PathRewriteParser(Options.Create(PathRewriteOptions.Default), new NullLogger<PathRewriteParser>());

        sut = new ManifestMerger(settingsBasedPathGenerator, pathRewriteParser, new NullLogger<ManifestMerger>());
    }

    [Fact]
    public void MergeManifest_ReturnsUnchanged_IfNullNQManifest()
    {
        var baseManifest = new Manifest { Id = "base" };

        var result = sut.MergeManifest(baseManifest, null, [], CustomerId, ManifestId);

        result.Should().Be(baseManifest);
        result.SeeAlso.Should().BeNullOrEmpty();
        result.Rendering.Should().BeNullOrEmpty();
        result.Annotations.Should().BeNullOrEmpty();
    }

    [Fact]
    public void MergeManifest_ReturnsUnchanged_IfNQManifestHasNullItems()
    {
        var baseManifest = new Manifest { Id = "base" };
        var nqManifest = new Manifest { Items = null };

        var result = sut.MergeManifest(baseManifest, nqManifest, [], CustomerId, ManifestId);

        result.Should().Be(baseManifest);
        result.SeeAlso.Should().BeNullOrEmpty();
    }

    [Fact]
    public void MergeManifest_ReturnsUnchanged_IfNQManifestHasNoItems()
    {
        var baseManifest = new Manifest { Id = "base" };
        var nqManifest = new Manifest { Items = [] };

        var result = sut.MergeManifest(baseManifest, nqManifest, [], CustomerId, ManifestId);

        result.Should().Be(baseManifest);
        result.SeeAlso.Should().BeNullOrEmpty();
    }

    [Fact]
    public void MergeManifest_ReturnsUnchanged_IfNoStubCanvasInNQ()
    {
        var baseManifest = new Manifest { Id = "base" };
        var otherAssetId = new AssetId(CustomerId, 1, "some-other-asset");
        var nqManifest = ManifestTestCreator.New()
            .WithCanvas(otherAssetId, c => c.WithImage())
            .Build();

        var result = sut.MergeManifest(baseManifest, nqManifest, [], CustomerId, ManifestId);

        result.Should().Be(baseManifest);
        result.SeeAlso.Should().BeNullOrEmpty();
    }

    [Fact]
    public void MergeManifest_SetsSeeAlso_FromStubCanvas()
    {
        var baseManifest = new Manifest { Id = "base" };
        var seeAlsoId = "https://example.com/mets.xml";
        var nqManifest = ManifestTestCreator.New()
            .WithCanvas(StubAssetId, c => c.WithImage().WithAdjunctSeeAlso(seeAlsoId))
            .Build();

        var result = sut.MergeManifest(baseManifest, nqManifest, [], CustomerId, ManifestId);

        result.SeeAlso.Should().ContainSingle(s => s.Id == seeAlsoId);
    }

    [Fact]
    public void MergeManifest_SetsRendering_FromStubCanvas()
    {
        var baseManifest = new Manifest { Id = "base" };
        var renderingId = "https://example.com/pdf";
        var nqManifest = ManifestTestCreator.New()
            .WithCanvas(StubAssetId, c => c.WithImage().WithAdjunctRendering(renderingId))
            .Build();

        var result = sut.MergeManifest(baseManifest, nqManifest, [], CustomerId, ManifestId);

        result.Rendering.Should().ContainSingle(r => r.Id == renderingId);
    }

    [Fact]
    public void MergeManifest_SetsAnnotations_FromStubCanvas()
    {
        var baseManifest = new Manifest { Id = "base" };
        var annotationId = "https://example.com/annotations/1";
        var nqManifest = ManifestTestCreator.New()
            .WithCanvas(StubAssetId, c => c.WithImage().WithAdjunctAnnotation(annotationId))
            .Build();

        var result = sut.MergeManifest(baseManifest, nqManifest, [], CustomerId, ManifestId);

        result.Annotations.Should().ContainSingle(a => a.Id == annotationId);
    }

    [Fact]
    public void MergeManifest_DeduplicatesById_WhenPropertyAlreadyExists()
    {
        var seeAlsoId = "https://example.com/mets.xml";
        var baseManifest = new Manifest
        {
            Id = "base",
            SeeAlso = [new ExternalResource("SeeAlso") { Id = seeAlsoId }]
        };
        var nqManifest = ManifestTestCreator.New()
            .WithCanvas(StubAssetId, c => c.WithImage().WithAdjunctSeeAlso(seeAlsoId))
            .Build();

        var result = sut.MergeManifest(baseManifest, nqManifest, [], CustomerId, ManifestId);

        result.SeeAlso.Should().ContainSingle(s => s.Id == seeAlsoId);
    }

    [Fact]
    public void MergeManifest_AppendsNewProperties_WhenDifferentIdAlreadyExists()
    {
        var existingId = "https://example.com/existing.xml";
        var newId = "https://example.com/new.xml";
        var baseManifest = new Manifest
        {
            Id = "base",
            SeeAlso = [new ExternalResource("SeeAlso") { Id = existingId }]
        };
        var nqManifest = ManifestTestCreator.New()
            .WithCanvas(StubAssetId, c => c.WithImage().WithAdjunctSeeAlso(newId))
            .Build();

        var result = sut.MergeManifest(baseManifest, nqManifest, [], CustomerId, ManifestId);

        result.SeeAlso.Should().HaveCount(2);
        result.SeeAlso.Should().Contain(s => s.Id == existingId);
        result.SeeAlso.Should().Contain(s => s.Id == newId);
    }
}
