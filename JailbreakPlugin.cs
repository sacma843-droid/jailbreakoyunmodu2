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

    // ctrev icin paylasimli hak sayaci (tum CT takimi icin ortak)
    private const int CtRevDefaultCharges = 3;
    private int _ctRevRemaining = CtRevDefaultCharges;

    public override void Load(bool hotReload)
    {
        // 1) /jointeam komutunu engelle
        AddCommandListener("jointeam", OnJoinTeamCommand);

        // Chat komutlarini dinle: !hucre1, !mute, !unmute, !ctrev0
        AddCommandListener("say", OnSayCommand);
        AddCommandListener("say_team", OnSayCommand);

        // Eventler
        RegisterEventHandler<EventPlayerSpawn>(OnPlayerSpawn);
        RegisterEventHandler<EventPlayerDeath>(OnPlayerDeath, HookMode.Post);
        RegisterEventHandler<EventRoundStart>(OnRoundStart);
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

    [GameEventHandler]
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

    // ================== Chat komutlari: !hucre1 / !mute / !unmute / !ctrev0 ==================
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

        // Diger tum mesajlar serbest, kisitlama yok
        return HookResult.Continue;
    }

    // ================== T takimi ses kisitlamasi: !mute / !unmute ==================
    private void HandleUnmute(CCSPlayerController caller, string arg)
    {
        // Arguman yoksa oyuncu kendi sesini aciyor (T takiminin varsayilan susturmayi kaldirmasi icin)
        if (string.IsNullOrEmpty(arg))
        {
            SetVoiceMuted(caller, false);
            caller.PrintToChat(" \x04[JB]\x01 Sesin acildi.");
            return;
        }

        // Baska birini hedeflemek icin @css/generic yetkisi gerekir
        if (!AdminManager.PlayerHasPermissions(caller, "@css/generic"))
        {
            caller.PrintToChat(" \x04[JB]\x01 Bu islem icin yetkin yok.");
            return;
        }

        var target = FindPlayerByName(arg);
        if (target == null)
        {
            caller.PrintToChat(" \x04[JB]\x01 Oyuncu bulunamadi.");
            return;
        }

        SetVoiceMuted(target, false);
        caller.PrintToChat($" \x04[JB]\x01 {target.PlayerName} adli oyuncunun sesi acildi.");
    }

    private void HandleMute(CCSPlayerController caller, string arg)
    {
        // !mute her zaman @css/generic yetkisi ister
        if (!AdminManager.PlayerHasPermissions(caller, "@css/generic"))
        {
            caller.PrintToChat(" \x04[JB]\x01 Bu islem icin yetkin yok.");
            return;
        }

        if (string.IsNullOrEmpty(arg))
        {
            caller.PrintToChat(" \x04[JB]\x01 Kullanim: !mute <oyuncu adi>");
            return;
        }

        var target = FindPlayerByName(arg);
        if (target == null)
        {
            caller.PrintToChat(" \x04[JB]\x01 Oyuncu bulunamadi.");
            return;
        }

        SetVoiceMuted(target, true);
        caller.PrintToChat($" \x04[JB]\x01 {target.PlayerName} susturuldu.");
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

    // ================== ctrev: paylasimli CT rev hakki sifirlama ==================
    private void HandleCtRevReset(CCSPlayerController caller)
    {
        if (!AdminManager.PlayerHasPermissions(caller, "@css/generic"))
        {
            caller.PrintToChat(" \x04[JB]\x01 Bu islem icin yetkin yok.");
            return;
        }

        _ctRevRemaining = CtRevDefaultCharges;
        Server.PrintToChatAll($" \x04[JB]\x01 CT rev haklari sifirlandi! Yeni hak: {CtRevDefaultCharges}");
    }

    // ================== 4/6/7) Spawnda silah sifirla + takima gore silah ver + ses ayari ==================
    private HookResult OnPlayerSpawn(EventPlayerSpawn @event, GameEventInfo info)
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

            // 6) T takimi sadece bicak + varsayilan olarak susturulmus
            if (player.Team == CsTeam.Terrorist)
            {
                player.GiveNamedItem("weapon_knife");
                SetVoiceMuted(player, true);
            }
            // 7) CT takimi varsayilan silahlari + sesi acik
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

    // ================== ctrev: CT olum aninda paylasimli hak varsa otomatik rev ==================
    private HookResult OnPlayerDeath(EventPlayerDeath @event, GameEventInfo info)
    {
        var victim = @event.Userid;
        if (victim == null || !victim.IsValid)
            return HookResult.Continue;

        if (victim.Team == CsTeam.CounterTerrorist && _ctRevRemaining > 0)
        {
            _ctRevRemaining--;
            int kalan = _ctRevRemaining;
            string victimName = victim.PlayerName;

            AddTimer(0.3f, () =>
            {
                if (victim.IsValid)
                {
                    victim.Respawn();
                }
            });

            Server.PrintToChatAll($" \x04[JB]\x01 {victimName} kisisi revlenmistir ve {kalan} hak kalmistir.");
        }

        return HookResult.Continue;
    }

    // ================== 5) !hucre1 ile kapi acma ==================
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

    // ================== 8) Round basi komutlari + ctrev haklarini yenile ==================
    private HookResult OnRoundStart(EventRoundStart @event, GameEventInfo info)
    {
        Server.ExecuteCommand("sv_gravity 800");
        Server.ExecuteCommand("mp_teammates_are_enemies 0");

        // Her round basinda CT'nin ortak rev hakki 3'e sifirlanir
        _ctRevRemaining = CtRevDefaultCharges;

        return HookResult.Continue;
    }
}
