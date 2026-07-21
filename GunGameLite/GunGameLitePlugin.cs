using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using Microsoft.Extensions.Logging;

namespace GunGameLite;

[MinimumApiVersion(371)]
public class GunGameLitePlugin : BasePlugin
{
    public override string ModuleAuthor => "creazy.eth";
    public override string ModuleName => "GunGameLite";
    public override string ModuleDescription => "A simple GunGame game mode for Counter-Strike 2.";
    public override string ModuleVersion => "0.1.0";

    // Defined now so helpers (Task 8) can reference it; the full body arrives in Task 9.
    public static GunGameLitePlugin? Instance { get; private set; }

    public override void Load(bool hotReload)
    {
        Instance = this;
        Logger.LogInformation("{Name} v{Version} loaded (stub)", ModuleName, ModuleVersion);
    }
}
