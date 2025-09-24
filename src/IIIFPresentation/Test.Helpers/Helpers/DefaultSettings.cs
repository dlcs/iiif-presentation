using DLCS;

namespace Test.Helpers.Helpers;

public static class DefaultSettings
{

    public static DlcsSettings DlcsSettings()
    {
        return  new DlcsSettings()
        {
            ApiUri = new Uri("https://dlcs.api"),
            OrchestratorUri = new Uri("https://dlcs.orchestrator")
        };
    }
}
