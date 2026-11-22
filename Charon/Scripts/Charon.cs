// SPDX-License-Identifier: GPL-3.0-only
// Feigned death and revival logic based on Nebula-Public's Busker by Dolly1016 and contributors.
using System;
using System.Collections.Generic;
using Nebula;
using Nebula.Configuration;
using Nebula.Extensions;
using Nebula.Game;
using Nebula.Map;
using Nebula.Modules;
using Nebula.Modules.ScriptComponents;
using Nebula.Roles.Impostor;
using Nebula.Utilities;
using Virial;
using Virial.Assignable;
using Virial.Compat;
using Virial.Components;
using Virial.Configuration;
using Virial.Events.Game;
using Virial.Game;
using Virial.Media;
using Virial.Text;

namespace NebulaCharon;

public sealed class Charon : DefinedSingleAbilityRoleTemplate<Charon.Ability>, DefinedRole, IAssignableDocument
{
    private const string Option = "options.role.charon.";
    private static readonly Color RoleColor = new((byte)51, (byte)153, (byte)0);
    private static readonly FloatConfiguration Cooldown = NebulaAPI.Configurations.Configuration(
        Option + "cooldown", (0f, 60f, 2.5f), 0f, FloatConfigurationDecorator.Second);
    private static readonly BoolConfiguration TimeLimit = NebulaAPI.Configurations.Configuration(
        Option + "timeLimit", false);
    private static readonly FloatConfiguration Duration = NebulaAPI.Configurations.Configuration(
        Option + "duration", (2.5f, 120f, 2.5f), 30f, FloatConfigurationDecorator.Second, () => TimeLimit);
    private static readonly BoolConfiguration HideFromVitals = NebulaAPI.Configurations.Configuration(
        Option + "hideFromVitals", false);
    private static readonly FloatConfiguration Required = NebulaAPI.Configurations.Configuration(
        Option + "required", (5f, 200f, 5f), 50f, FloatConfigurationDecorator.Ratio);
    private static readonly FloatConfiguration Surplus = NebulaAPI.Configurations.Configuration(
        Option + "surplus", (0f, 100f, 5f), 20f, FloatConfigurationDecorator.Ratio);
    private static readonly FloatConfiguration Increase = NebulaAPI.Configurations.Configuration(
        Option + "increase", (0f, 4f, 0.125f), 1f, FloatConfigurationDecorator.Ratio);
    private static readonly FloatConfiguration Decrease = NebulaAPI.Configurations.Configuration(
        Option + "decrease", (0f, 4f, 0.125f), 1f, FloatConfigurationDecorator.Ratio);

    private static readonly RoleTeam Team = NebulaAPI.Preprocessor!.CreateTeam("teams.charon", RoleColor, TeamRevealType.OnlyMe);
    private static readonly GameEnd CharonWin = NebulaAPI.Preprocessor!.CreateEnd(
        "charon", RoleColor, priority: int.MaxValue);
    private static readonly Image FeignImage = SpriteLoader.FromResource("Nebula.Resources.Buttons.BuskPseudocideButton.png", 115f);
    private static readonly Image ReviveImage = SpriteLoader.FromResource("Nebula.Resources.Buttons.BuskReviveButton.png", 115f);

    private static readonly RemoteProcess<(byte PlayerId, bool Qualified, bool Feigning)> ShareQualification = new(
        "NebulaCharon.ShareQualification", (message, isLocal) =>
        {
            if (isLocal) return;
            if (NebulaAPI.CurrentGame is not { } game) return;
            foreach (var player in game.GetAllPlayers())
            {
                if (player.PlayerId != message.PlayerId) continue;
                var ability = player.Role?.GetAbility<Ability>();
                if (ability != null)
                {
                    ability.Qualified = message.Qualified;
                    ability.Feigning = message.Feigning;
                }
                break;
            }
        });

    public static readonly Charon MyRole = new();

    private Charon() : base("charon", RoleColor, RoleCategory.NeutralRole, Team,
        [Cooldown, TimeLimit, Duration, HideFromVitals, Required, Surplus, Increase, Decrease])
    {
        ConfigurationHolder?.AddTags(ConfigurationTags.TagDifficult, ConfigurationTags.TagFunny);
    }

    public override Ability CreateAbility(Player player, int[] arguments) => new(player);
    bool IAssignableDocument.HasAbility => true;
    bool IAssignableDocument.HasWinCondition => true;

    IEnumerable<AssignableDocumentImage> IAssignableDocument.GetDocumentImages()
    {
        yield return new(FeignImage, "role.charon.ability.pseudocide");
        yield return new(ReviveImage, "role.charon.ability.revive");
    }

    public sealed class Ability : AbstractPlayerAbility, IPlayerAbility
    {
        private static float Capacity => (float)Required + Surplus;
        private ModAbilityButton feign = null!;
        private ModAbilityButton revive = null!;
        private global::DeadBody? corpse;
        private Vector2 lastPosition;
        private bool killPending;
        private bool growing;
        private float farthest;
        private float styx;

        internal bool Qualified { get; set; }
        internal bool Feigning { get; set; }

        private bool CanWin => Qualified && (!MyPlayer.IsDead ||
            (Feigning && MyPlayer.PlayerState == PlayerStates.Pseudocide &&
             !MeetingHud.Instance && !ExileController.Instance && Helpers.GetDeadBody(MyPlayer.PlayerId) != null));

        public Ability(Player player) : base(player)
        {
            if (!AmOwner) return;

            feign = NebulaAPI.Modules.AbilityButton(this, isArrangedAsKillButton: true)
                .BindKey(VirtualKeyInput.Ability).SetImage(FeignImage).SetLabel("pseudocide");
            feign.Availability = _ => MyPlayer.CanMove && !killPending;
            feign.Visibility = _ => !MyPlayer.IsDead;
            feign.CoolDownTimer = NebulaAPI.Modules.Timer(this, Cooldown).SetAsAbilityTimer().Start();
            feign.OnClick = _ => BeginFeigning();

            revive = NebulaAPI.Modules.AbilityButton(this, isArrangedAsKillButton: true)
                .BindKey(VirtualKeyInput.Ability).SetImage(ReviveImage).SetLabel("revive");
            revive.Visibility = _ => Feigning && Helpers.GetDeadBody(MyPlayer.PlayerId) != null;
            revive.Availability = _ => MyPlayer.CanMove &&
                MapData.GetCurrentMapData().CheckMapArea(MyPlayer.TruePosition);
            if (TimeLimit) revive.EffectTimer = NebulaAPI.Modules.Timer(this, Duration);
            revive.PlayFlashWhile = _ => true;
            revive.OnClick = _ => Revive();
            revive.OnEffectEnd = _ => FinishFeigning(died: true);
            revive.OnMeeting = _ => CancelFeigning();

            // Leave the gauge icon slot empty.
            var noIcon = new WrapSpriteLoader(() => null!);
            new AbilityGauge(Capacity, (float)Required, 100f, noIcon, noIcon,
                () => styx, () => growing).Register(this);
        }

        private void BeginFeigning()
        {
            killPending = true;
            NebulaManager.Instance.ScheduleDelayAction(() =>
            {
                if (IsDeadObject || MyPlayer.IsDead || MeetingHud.Instance || ExileController.Instance)
                {
                    killPending = false;
                    return;
                }

                using var section = RPCRouter.CreateSection("CharonPseudocide");
                if (HideFromVitals) MyPlayer.GainAttribute(PlayerAttributes.BuskerEffect, float.MaxValue, false, 0);
                SetLocalFeigning(true);
                MyPlayer.MurderPlayer(MyPlayer, PlayerStates.Pseudocide, null,
                    KillParameter.WithDeadBody, KillCondition.NormalKill, result =>
                    {
                        killPending = false;
                        if (result != KillResult.Kill)
                        {
                            ClearFeigningStatus();
                            return;
                        }

                        if (IsDeadObject)
                        {
                            ClearFeigningStatus();
                            NebulaGameManager.RpcTryAssignGhostRole.Invoke(MyPlayer.Unbox());
                            return;
                        }

                        Feigning = true;
                        ShareQualification.Invoke((MyPlayer.PlayerId, Qualified, Feigning));
                        corpse = null;
                        farthest = 0f;
                        lastPosition = MyPlayer.TruePosition;
                        if (TimeLimit) revive.StartEffect();
                        if (MeetingHud.Instance || ExileController.Instance) CancelFeigning();
                    });
            });
        }

        private void Revive()
        {
            if (!Feigning || Helpers.GetDeadBody(MyPlayer.PlayerId) == null) return;

            using var section = RPCRouter.CreateSection("CharonRevive");
            FinishFeigning(died: false);
            MyPlayer.Revive(null, MyPlayer.Position, true, false);
            MyPlayer.VanillaPlayer.ModDive(false);
            revive.InterruptEffect();
            feign.StartCoolDown();
        }

        private static void SetLocalFeigning(bool active)
        {
            if (NebulaGameManager.Instance is { } manager)
                manager.LocalStatus.InBuskerPseudocide = active;
        }

        private void ClearFeigningStatus()
        {
            if (HideFromVitals) MyPlayer.RemoveAttribute(PlayerAttributes.BuskerEffect);
            SetLocalFeigning(false);
        }

        private void FinishFeigning(bool died)
        {
            if (!Feigning) return;

            Feigning = false;
            growing = false;
            corpse = null;
            ClearFeigningStatus();
            ShareQualification.Invoke((MyPlayer.PlayerId, Qualified, Feigning));
            if (!died || !MyPlayer.IsDead) return;

            MyPlayer.GetModule<BalloonHolder>()?.OnDeadFinally();
            NebulaGameManager.RpcTryAssignGhostRole.Invoke(MyPlayer.Unbox());
        }

        private void CancelFeigning()
        {
            FinishFeigning(died: true);
            revive.InterruptEffect();
        }

        void IGameOperator.OnReleased()
        {
            if (AmOwner) FinishFeigning(died: true);
        }

        private void UpdateStyx(GameUpdateEvent ev)
        {
            if (!AmOwner || MyPlayer.IsDisconnected) return;

            growing = false;
            if (MeetingHud.Instance || ExileController.Instance) return;
            if (!Feigning)
            {
                SetStyx(styx - ev.DeltaTime * (float)Decrease);
                return;
            }

            if (!MyPlayer.IsDead || MyPlayer.PlayerState != PlayerStates.Pseudocide)
            {
                CancelFeigning();
                return;
            }

            var body = Helpers.GetDeadBody(MyPlayer.PlayerId);
            if (body == null)
            {
                if (corpse is not null) CancelFeigning();
                return;
            }

            var position = MyPlayer.TruePosition;
            Vector2 bodyPosition = body.TruePosition;
            if (corpse is null)
            {
                corpse = body;
                farthest = bodyPosition.Distance(lastPosition);
            }

            var distance = position.Distance(bodyPosition);
            if (position.Distance(lastPosition) > 0f && distance > lastPosition.Distance(bodyPosition) && distance > farthest)
            {
                float gain = (distance - farthest) * (float)Increase;
                farthest = distance;
                growing = gain > 0f && styx < Capacity;
                SetStyx(styx + gain);
            }

            lastPosition = position;
        }

        private void SetStyx(float value)
        {
            styx = Math.Clamp(value, 0f, Capacity);
            bool qualified = styx >= (float)Required;
            if (qualified == Qualified) return;

            Qualified = qualified;
            ShareQualification.Invoke((MyPlayer.PlayerId, qualified, Feigning));
        }

        private void OnGameEnding(EndCriteriaMetEvent ev) => OverrideWinner(ev);
        private void OnGameEndOverwritten(EndCriteriaOverwrittenEvent ev) => OverrideWinner(ev);

        private static void OverrideWinner(EndCriteriaMetBaseEvent ev)
        {
            if (!ev.OriginalGameEnd.AllowWin || ev.OriginalGameEnd == CharonWin) return;

            int winners = 0;
            foreach (var player in NebulaAPI.CurrentGame!.GetAllPlayers())
            {
                if (!player.IsDisconnected && player.Role?.Role == MyRole &&
                    player.Role.GetAbility<Ability>()?.CanWin == true)
                    winners |= 1 << player.PlayerId;
            }

            if (winners != 0) ev.TryOverwriteEnd(CharonWin, GameEndReason.Special, winners);
        }
    }
}
