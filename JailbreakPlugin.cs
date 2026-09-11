using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.Modules.Timers;

namespace JailbreakBase;

public class JailbreakPlugin : BasePlugin
{
    public override string ModuleName => "Jailbreak Base";
    public override string ModuleVersion => "1.0.0";
    public override string ModuleAuthor => "Claude";
    public override string ModuleDescription => "Temel Jailbreak oyun modu ozellikleri (CounterStrikeSharp)";

    public override void Load(bool hotReload)
    {
        // 1) /jointeam komutunu engelle
        AddCommandListener("jointeam", OnJoinTeamCommand);

        // !hucre1 komutu icin chat mesajlarini dinle
        AddCommandListener("say", OnSayCommand);
        AddCommandListener("say_team", OnSayCommand);

        // Eventler
        RegisterEventHandler<EventPlayerSpawn>(OnPlayerSpawn);
        RegisterEventHandler<EventRoundStart>(OnRoundStart);

        // Oyuncu sunucuya girince otomatik T yap
        RegisterListener<Listeners.OnClientPutInServer>(OnClientPutInServer);
    }

    // ================== 1) /jointeam ENGELLE ==================
    private HookResult OnJoinTeamCommand(CCSPlayerController? player, CommandInfo info)
    {
        if (player == null || !player.IsValid)
            return HookResult.Continue;

        if (player.Team != CsTeam.Terrorist)
        {
            player.ChangeTeam(CsTeam.Terrorist);
        }
        return HookResult.Stop;
    }

    private void OnClientPutInServer(int slot)
    {
        var player = Utilities.GetPlayerFromSlot(slot);
        if (player == null) return;

        AddTimer(0.3f, () =>
        {
            if (player.IsValid && (player.Team == CsTeam.None || player.Team == CsTeam.Spectator))
            {
                player.ChangeTeam(CsTeam.Terrorist);
                player.Respawn();
            }
        });
    }

    // ================== !hucre1 komutuyla kapi acma ==================
    private HookResult OnSayCommand(CCSPlayerController? player, CommandInfo info)
    {
        if (player == null || !player.IsValid)
            return HookResult.Continue;

        string message = (info.GetArg(1) ?? "").Trim();

        if (message.Equals("!hucre1", StringComparison.OrdinalIgnoreCase))
        {
            OpenAllDoors();
            return HookResult.Handled;
        }

        return HookResult.Continue;
    }

    // ================== 4/6/7) Spawnda silah sifirla + takima gore silah ver ==================
    private HookResult OnPlayerSpawn(EventPlayerSpawn @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (player == null || !player.IsValid || !player.PawnIsAlive)
            return HookResult.Continue;

        AddTimer(0.1f, () =>
        {
            if (!player.IsValid || !player.PawnIsAlive) return;

            // Tum silahlari temizle
            player.RemoveWeapons();

            // T takimi sadece bicak
            if (player.Team == CsTeam.Terrorist)
            {
                player.GiveNamedItem("weapon_knife");
            }
            // CT takimi varsayilan silahlar
            else if (player.Team == CsTeam.CounterTerrorist)
            {
                player.GiveNamedItem("weapon_deagle");
                player.GiveNamedItem("weapon_ak47");
                player.GiveNamedItem("weapon_knife");
            }
        });

        return HookResult.Continue;
    }

    // ================== !hucre1 ile kapi acma ==================
    private void OpenAllDoors()
    {
        foreach (var door in Utilities.FindAllEntitiesByDesignerName<CBaseEntity>("prop_door_rotating"))
        {
            door.AcceptInput("Open");
        }
        foreach (var door in Utilities.FindAllEntitiesByDesignerName<CBaseEntity>("func_door"))
        {
            door.AcceptInput("Open");
        }
        foreach (var door in Utilities.FindAllEntitiesByDesignerName<CBaseEntity>("func_door_rotating"))
        {
            door.AcceptInput("Open");
        }
        foreach (var btn in Utilities.FindAllEntitiesByDesignerName<CBaseEntity>("func_button"))
        {
            btn.AcceptInput("Press");
        }
    }

    // ================== 8) Round basi komutlari ==================
    private HookResult OnRoundStart(EventRoundStart @event, GameEventInfo info)
    {
        Server.ExecuteCommand("sv_gravity 800");
        Server.ExecuteCommand("mp_teammates_are_enemies 0");
        return HookResult.Continue;
    }
}
