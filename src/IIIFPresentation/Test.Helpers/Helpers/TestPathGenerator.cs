using Repository.Paths;

namespace Test.Helpers.Helpers;

public class TestPathGenerator(IPresentationPathGenerator presentationPathGenerator) : PathGeneratorBase(presentationPathGenerator)
{
    protected override Uri DlcsApiUrl => new("https://dlcs.test");
}
