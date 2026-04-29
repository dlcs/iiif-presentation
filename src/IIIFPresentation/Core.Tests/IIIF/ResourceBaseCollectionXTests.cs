using Core.IIIF;
using IIIF.Presentation.V3;

namespace Core.Tests.IIIF;

public class ResourceBaseCollectionXTests
{
    private class TestItem : ResourceBase
    {
        public TestItem(string id) { Id = id; }

        public override string Type => "TestItem";
    }

    [Fact]
    public void AddDistinctById_AddsAllItemsWhenTargetEmpty()
    {
        var target = new List<TestItem>();
        var source = new[] { new TestItem("id1"), new TestItem("id2") };

        target.AddDistinctById(source);

        target.Should().HaveCount(2);
        target.Select(x => x.Id).Should().ContainInOrder("id1", "id2");
    }

    [Fact]
    public void AddDistinctById_ExcludesDuplicateIds()
    {
        var target = new List<TestItem> { new("id1") };
        var source = new[] { new TestItem("id1"), new TestItem("id2") };

        target.AddDistinctById(source);

        target.Should().HaveCount(2);
        target.Select(x => x.Id).Should().ContainInOrder("id1", "id2");
    }

    [Fact]
    public void AddDistinctById_IgnoresNullSource()
    {
        var target = new List<TestItem> { new("id1") };

        target.AddDistinctById(null);

        target.Should().HaveCount(1);
        target[0].Id.Should().Be("id1");
    }

    [Fact]
    public void AddDistinctById_ExcludesAllDuplicates()
    {
        var target = new List<TestItem> { new("id1"), new("id2") };
        var source = new[] { new TestItem("id1"), new TestItem("id2"), new TestItem("id3") };

        target.AddDistinctById(source);

        target.Should().HaveCount(3);
        target.Select(x => x.Id).Should().ContainInOrder("id1", "id2", "id3");
    }
}
