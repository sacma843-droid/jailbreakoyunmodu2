using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
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

        // 3) Sadece CT konusabilsin
        AddCommandListener("say", OnSayCommand);
        AddCommandListener("say_team", OnSayCommand);

        // Listener (attribute ile de yapılabilir, ama burada açıkça kaydettik)
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
        return HookResult.Stop; // komutu tamamen engelle
    }

    // ================== 2) Otomatik T takimina katilma ==================
    [GameEventHandler]
    public HookResult OnPlayerTeam(EventPlayerTeam @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (player == null || !player.IsValid)
            return HookResult.Continue;

        // Oyuncu CT'ye gecmeye calisiyorsa T'ye geri al
        if (@event.Team == (int)CsTeam.CounterTerrorist)
        {
            AddTimer(0.1f, () =>
            {
                if (player.IsValid)
                    player.ChangeTeam(CsTeam.Terrorist);
            });
        }
        return HookResult.Continue;
    }

    public void OnClientPutInServer(int slot)
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

    // ================== 3) Sadece CT konusabilsin ==================
    private HookResult OnSayCommand(CCSPlayerController? player, CommandInfo info)
    {
        if (player == null || !player.IsValid)
            return HookResult.Continue;

        if (player.Team != CsTeam.CounterTerrorist)
        {
            player.PrintToChat(" \x04[JB]\x01 Sadece CT takimi konusabilir!");
            return HookResult.Stop;
        }
        return HookResult.Continue;
    }

    // ================== 4/6/7) Spawnda silah sifirla + takima gore silah ver ==================
    [GameEventHandler]
    public HookResult OnPlayerSpawn(EventPlayerSpawn @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (player == null || !player.IsValid || player.PawnIsAlive == false)
            return HookResult.Continue;

        AddTimer(0.1f, () =>
        {
            if (!player.IsValid || player.PawnIsAlive == false) return;

            var pawn = player.PlayerPawn.Value;
            if (pawn == null) return;

            // Tum silahlari at
            pawn.WeaponServices?.RemoveWeapons();

            // 6) T takimi sadece bicak
            if (player.Team == CsTeam.Terrorist)
            {
                player.GiveNamedItem("weapon_knife");
            }
            // 7) CT takimi varsayilan silahlari
            else if (player.Team == CsTeam.CounterTerrorist)
            {
                player.GiveNamedItem("weapon_deagle");
                player.GiveNamedItem("weapon_ak47");
                player.GiveNamedItem("weapon_knife");
            }
        });

        return HookResult.Continue;
    }

    // ================== 5) CT kalmayinca kapilar acilsin ==================
    [GameEventHandler(HookMode.Post)]
    public HookResult OnPlayerDeath(EventPlayerDeath @event, GameEventInfo info)
    {
        CheckCtCount();
        return HookResult.Continue;
    }

    [GameEventHandler(HookMode.Post)]
    public HookResult OnPlayerDisconnect(EventPlayerDisconnect @event, GameEventInfo info)
    {
        AddTimer(0.2f, CheckCtCount);
        return HookResult.Continue;
    }

    private void CheckCtCount()
    {
        int ctCount = Utilities.GetPlayers()
            .Count(p => p.IsValid && p.PawnIsAlive && p.Team == CsTeam.CounterTerrorist);

        if (ctCount == 0)
        {
            OpenAllDoors();
            Server.PrintToChatAll(" \x04[JB]\x01 Sunucuda hic CT kalmadi! Tum kapilar aciliyor...");
        }
    }

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
    [GameEventHandler]
    public HookResult OnRoundStart(EventRoundStart @event, GameEventInfo info)
    {
        Server.ExecuteCommand("sv_gravity 800");
        Server.ExecuteCommand("mp_teammates_are_enemies 0");
        return HookResult.Continue;
    }
}
