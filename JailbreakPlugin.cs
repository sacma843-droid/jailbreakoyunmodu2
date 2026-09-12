using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.Modules.Timers;

namespace JailbreakBase;

public class JailbreakPlugin : BasePlugin
{
    public override string ModuleName => "Jailbreak Base";
    public override string ModuleVersion => "1.1.0";
    public override string ModuleAuthor => "Claude";
    public override string ModuleDescription => "Temel Jailbreak oyun modu ozellikleri (CounterStrikeSharp)";

    private const int CtRevDefaultCharges = 3;
    private int _ctRevRemaining = CtRevDefaultCharges;

    public override void Load(bool hotReload)
    {
        AddCommandListener("jointeam", OnJoinTeamCommand);
        AddCommandListener("say", OnSayCommand);
        AddCommandListener("say_team", OnSayCommand);

        RegisterEventHandler<EventPlayerSpawn>(OnPlayerSpawn);
        RegisterEventHandler<EventPlayerDeath>(OnPlayerDeath, HookMode.Post);
        RegisterEventHandler<EventRoundStart>(OnRoundStart);

        RegisterListener<Listeners.OnClientPutInServer>(OnClientPutInServer);
    }

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

    private HookResult OnSayCommand(CCSPlayerController? player, CommandInfo info)
    {
        if (player == null || !player.IsValid)
            return HookResult.Continue;

        string full = (info.GetArg(1) ?? "").Trim();
        if (full.Length == 0)
            return HookResult.Continue;

        var parts = full.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        string cmd = parts[0].ToLowerInvariant();
        string arg = parts.Length > 1 ? parts[1].Trim() : "";

        switch (cmd)
        {
            case "!hucre1":
                OpenAllDoors();
                return HookResult.Handled;

            case "!unmute":
                HandleUnmute(player, arg);
                return HookResult.Handled;

            case "!mute":
                HandleMute(player, arg);
                return HookResult.Handled;

            case "!ctrev0":
                HandleCtRevReset(player);
                return HookResult.Handled;
        }

        return HookResult.Continue;
    }

    private void HandleUnmute(CCSPlayerController caller, string arg)
    {
        if (string.IsNullOrEmpty(arg))
        {
            SetVoiceMuted(caller, false);
            caller.PrintToChat($" {ChatColors.Green}[JB]{ChatColors.Default} Sesin açıldı.");
            return;
        }

        if (!AdminManager.PlayerHasPermissions(caller, "@css/generic"))
        {
            caller.PrintToChat($" {ChatColors.Red}[JB]{ChatColors.Default} Bu işlem için yetkin yok.");
            return;
        }

        var target = FindPlayerByName(arg);
        if (target == null)
        {
            caller.PrintToChat($" {ChatColors.Red}[JB]{ChatColors.Default} Oyuncu bulunamadı.");
            return;
        }

        SetVoiceMuted(target, false);
        caller.PrintToChat($" {ChatColors.Green}[JB]{ChatColors.Default} {ChatColors.Lime}{target.PlayerName}{ChatColors.Default} adlı oyuncunun sesi açıldı.");
    }

    private void HandleMute(CCSPlayerController caller, string arg)
    {
        if (!AdminManager.PlayerHasPermissions(caller, "@css/generic"))
        {
            caller.PrintToChat($" {ChatColors.Red}[JB]{ChatColors.Default} Bu işlem için yetkin yok.");
            return;
        }

        if (string.IsNullOrEmpty(arg))
        {
            caller.PrintToChat($" {ChatColors.Red}[JB]{ChatColors.Default} Kullanım: !mute <oyuncu adı>");
            return;
        }

        var target = FindPlayerByName(arg);
        if (target == null)
        {
            caller.PrintToChat($" {ChatColors.Red}[JB]{ChatColors.Default} Oyuncu bulunamadı.");
            return;
        }

        SetVoiceMuted(target, true);
        caller.PrintToChat($" {ChatColors.Red}[JB]{ChatColors.Default} {ChatColors.Lime}{target.PlayerName}{ChatColors.Default} susturuldu.");
    }

    private void SetVoiceMuted(CCSPlayerController player, bool muted)
    {
        player.VoiceFlags = muted ? VoiceFlags.Muted : VoiceFlags.Normal;
    }

    private CCSPlayerController? FindPlayerByName(string partialName)
    {
        return Utilities.GetPlayers()
            .FirstOrDefault(p => p.IsValid && p.PlayerName.Contains(partialName, StringComparison.OrdinalIgnoreCase));
    }

    private void HandleCtRevReset(CCSPlayerController caller)
    {
        if (!AdminManager.PlayerHasPermissions(caller, "@css/generic"))
        {
            caller.PrintToChat($" {ChatColors.Red}[JB]{ChatColors.Default} Bu işlem için yetkin yok.");
            return;
        }

        _ctRevRemaining = CtRevDefaultCharges;
        Server.PrintToChatAll($" {ChatColors.Green}[JB]{ChatColors.Default} CT rev hakları sıfırlandı! Yeni hak: {ChatColors.Gold}{CtRevDefaultCharges}");
    }

    private HookResult OnPlayerSpawn(EventPlayerSpawn @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (player == null || !player.IsValid || !player.PawnIsAlive)
            return HookResult.Continue;

        AddTimer(0.1f, () =>
        {
            if (!player.IsValid || !player.PawnIsAlive) return;

            player.RemoveWeapons();

            if (player.Team == CsTeam.Terrorist)
            {
                player.GiveNamedItem("weapon_knife");
                SetVoiceMuted(player, true);
            }
            else if (player.Team == CsTeam.CounterTerrorist)
            {
                player.GiveNamedItem("weapon_deagle");
                player.GiveNamedItem("weapon_ak47");
                player.GiveNamedItem("weapon_knife");
                SetVoiceMuted(player, false);
            }
        });

        return HookResult.Continue;
    }

    private HookResult OnPlayerDeath(EventPlayerDeath @event, GameEventInfo info)
    {
        var victim = @event.Userid;
        if (victim == null || !victim.IsValid)
            return HookResult.Continue;

        if (victim.Team != CsTeam.CounterTerrorist || _ctRevRemaining <= 0)
            return HookResult.Continue;

        _ctRevRemaining--;
        int kalan = _ctRevRemaining;
        string victimName = victim.PlayerName;

        AddTimer(1.0f, () =>
        {
            if (!victim.IsValid || victim.PawnIsAlive) return;

            try
            {
                victim.Respawn();
                Server.PrintToChatAll($" {ChatColors.Green}[JB]{ChatColors.Default} {ChatColors.Lime}{victimName}{ChatColors.Default} kişisi {ChatColors.Gold}revlenmiştir{ChatColors.Default} ve {ChatColors.Yellow}{kalan}{ChatColors.Default} hak kalmıştır.");
            }
            catch { }
        });

        return HookResult.Continue;
    }

    private void OpenAllDoors()
    {
        foreach (var door in Utilities.FindAllEntitiesByDesignerName<CBaseEntity>("prop_door_rotating"))
            door.AcceptInput("Open");

        foreach (var door in Utilities.FindAllEntitiesByDesignerName<CBaseEntity>("func_door"))
            door.AcceptInput("Open");

        foreach (var door in Utilities.FindAllEntitiesByDesignerName<CBaseEntity>("func_door_rotating"))
            door.AcceptInput("Open");

        foreach (var btn in Utilities.FindAllEntitiesByDesignerName<CBaseEntity>("func_button"))
            btn.AcceptInput("Press");
    }

    private HookResult OnRoundStart(EventRoundStart @event, GameEventInfo info)
    {
        Server.ExecuteCommand("sv_gravity 800");
        Server.ExecuteCommand("mp_teammates_are_enemies 0");

        _ctRevRemaining = CtRevDefaultCharges;

        return HookResult.Continue;
    }
}
