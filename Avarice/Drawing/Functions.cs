﻿using Dalamud.Game.ClientState.Objects.Types;
using ECommons.GameFunctions;
using ECommons.GameHelpers;
using ECommons.MathHelpers;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using static Avarice.Drawing.DrawFunctions;
using static Avarice.Util;

namespace Avarice.Drawing;

internal static unsafe class Functions
{
	internal static void DrawTankMiddle()
	{
		if (!P.currentProfile.EnableTankMiddle && !P.currentProfile.EnableDutyMiddle)
		{
			return; //get out early
		}

		if (Player.Available && Util.TryAutoDetectMiddleOfArena(out Vector3 mid))
		{
			IEnumerable<ExtraPoint> points = P.config.DutyMiddleExtras.Where(x => x.TerritoryType == Svc.ClientState.TerritoryType);
			if (P.currentProfile.EnableTankMiddle && Svc.Targets.Target is IBattleNpc bnpc)
			{
				float distance = Vector3.Distance(mid, bnpc.Position);
				foreach (ExtraPoint x in points)
				{
					float addDistance = Vector3.Distance(x.Position, bnpc.Position);
					if (addDistance < distance)
					{
						distance = addDistance;
					}
				}
				Vector4 col = distance > P.config.DutyMidRadius ? P.config.UncenteredPixelColor : P.config.CenteredPixelColor;
				Util.DrawDot(bnpc.Position, P.config.CenterPixelThickness, col);
			}
			if (P.currentProfile.EnableDutyMiddle)
			{
				Util.DrawDot(mid, P.config.CenterPixelThickness, P.config.DutyMidPixelCol);
				foreach (ExtraPoint x in points)
				{
					Util.DrawDot(x.Position, P.config.CenterPixelThickness, P.config.DutyMidPixelCol);
				}
			}
		}
	}

	internal static void DrawFrontalPosition(IGameObject go)
	{
		if (go is IBattleNpc bnpc && bnpc.IsHostile() &&
				(!P.currentProfile.FrontStand || GetDirection(bnpc) == CardinalDirection.North))
		{
			if (P.currentProfile.VLine && P.currentProfile.FrontStand)
			{
				(int min, int max) = Get18PieForAngle(GetAngle(bnpc));
				ActorConeXZ(bnpc, bnpc.HitboxRadius + GetConfiguredRadius(), Maths.Radians(min), Maths.Radians(max), P.currentProfile.FrontSegmentIndicator);
			}
			else
			{
				ActorConeXZ(bnpc, bnpc.HitboxRadius + GetConfiguredRadius(), Maths.Radians(-45), Maths.Radians(45), P.currentProfile.FrontSegmentIndicator);
			}
		}
	}

	internal static void DrawCurrentPos(IBattleNpc bnpc)
	{
		float angle = GetAngle(bnpc);
		CardinalDirection direction = MathHelper.GetCardinalDirection(angle);
		if (direction == CardinalDirection.North)
		{
			return;
		}

		(int min, int max) = Is18(direction) ? Get18PieForAngle(angle) : GetAngleRangeForDirection(direction);
		ActorConeXZ(bnpc, bnpc.HitboxRadius + GetConfiguredRadius(), Maths.Radians(min), Maths.Radians(max),
			direction == CardinalDirection.South ? P.currentProfile.CurrentPieSettings : P.currentProfile.CurrentPieSettingsFlank);
	}

	internal static bool Is18(CardinalDirection direction)
	{
		if (direction is CardinalDirection.North or CardinalDirection.South)
		{
			return P.currentProfile.VLine;
		}
		else
		{
			return P.currentProfile.HLine;
		}
	}

	internal static void DrawSegmentedCircle(IBattleNpc bnpc, float addRadius, bool lines)
	{
		float radius = bnpc.HitboxRadius + addRadius;

		Brush nColor = P.currentProfile.SameColor ?
			P.currentProfile.MaxMeleeSettingsN with { Color = P.currentProfile.FrontSegmentIndicator.Fill with { W = 1f } } :
			P.currentProfile.MaxMeleeSettingsN;
		ActorConeXZ(bnpc, radius, Maths.Radians(-45), Maths.Radians(45), nColor, lines);

		Brush sColor = P.currentProfile.SameColor ?
			P.currentProfile.MaxMeleeSettingsN with { Color = P.currentProfile.CurrentPieSettings.Fill with { W = 1f } } :
			P.currentProfile.MaxMeleeSettingsN with { Color = P.currentProfile.MaxMeleeSettingsS };
		ActorConeXZ(bnpc, radius, Maths.Radians(180 - 45), Maths.Radians(180 + 45), sColor, lines);

		Brush eColor = P.currentProfile.SameColor ?
			P.currentProfile.MaxMeleeSettingsN with { Color = P.currentProfile.CurrentPieSettingsFlank.Fill with { W = 1f } } :
			P.currentProfile.MaxMeleeSettingsN with { Color = P.currentProfile.MaxMeleeSettingsE };
		ActorConeXZ(bnpc, radius, Maths.Radians(270 - 45), Maths.Radians(270 + 45), eColor, lines);

		Brush wColor = P.currentProfile.SameColor ?
			P.currentProfile.MaxMeleeSettingsN with { Color = P.currentProfile.CurrentPieSettingsFlank.Fill with { W = 1f } } :
			P.currentProfile.MaxMeleeSettingsN with { Color = P.currentProfile.MaxMeleeSettingsW };
		ActorConeXZ(bnpc, radius, Maths.Radians(90 - 45), Maths.Radians(90 + 45), wColor, lines);

		if (P.currentProfile.VLine)
		{
			ActorLineXZ(bnpc, radius, Maths.Radians(0), nColor);
			ActorLineXZ(bnpc, radius, Maths.Radians(180), sColor);
		}
		if (P.currentProfile.HLine)
		{
			ActorLineXZ(bnpc, radius, Maths.Radians(270), wColor);
			ActorLineXZ(bnpc, radius, Maths.Radians(90), eColor);
		}
	}

	private static bool MnkIsRear(IBattleNpc bnpc)
	{
		return Svc.ClientState.LocalPlayer.StatusList.Any(x => x.StatusId.EqualsAny(109u, 110u))
			&& (!bnpc.StatusList.TryGetFirst(x => x.StatusId.EqualsAny(246u, 3001u), out Dalamud.Game.ClientState.Statuses.Status status) || status.RemainingTime < P.currentProfile.MnkDemolish);
	}

	private static int? TrickAttackCDGroup = null;
	private static bool NinRearTrickAttackAvailable()
	{
		if (!P.currentProfile.NinRearForTrickAttack)
		{
			return false;
		}

		TrickAttackCDGroup ??= Svc.Data.GetExcelSheet<Lumina.Excel.GeneratedSheets.Action>().GetRow(2258).CooldownGroup - 1;
		return ActionManager.Instance()->GetRecastGroupDetail(TrickAttackCDGroup.Value)->IsActive == 0
			&& Svc.ClientState.LocalPlayer.StatusList.Any(x => x.StatusId.EqualsAny(507u, 614u));
	}

	internal static void DrawAnticipatedPos(IBattleNpc bnpc)
	{
		void DrawRear()
		{
			ActorConeXZ(bnpc, bnpc.HitboxRadius + GetSkillRadius(), Maths.Radians(180 - 45), Maths.Radians(180 + 45), P.currentProfile.AnticipatedPieSettings);
			P.PositionalStatus[0] = Framework.Instance()->FrameCounter;
			P.PositionalStatus[1] = 1;
		}

		void DrawSides()
		{
			ActorConeXZ(bnpc, bnpc.HitboxRadius + GetSkillRadius(), Maths.Radians(270 - 45), Maths.Radians(270 + 45), P.currentProfile.AnticipatedPieSettingsFlank);
			ActorConeXZ(bnpc, bnpc.HitboxRadius + GetSkillRadius(), Maths.Radians(90 - 45), Maths.Radians(90 + 45), P.currentProfile.AnticipatedPieSettingsFlank);
			P.PositionalStatus[0] = Framework.Instance()->FrameCounter;
			P.PositionalStatus[1] = 2;
		}

		if (IsMNKAnticipatedRear() || IsDRGAnticipatedRear() || IsNINAnticipatedRear()
			|| IsSAMAnticipatedRear() || IsRPRAnticipatedRear() || IsVPRAnticipatedRear())
		{
			DrawRear();
		}

		if (IsMNKAnticipatedFlank() || IsDRGAnticipatedFlank() || IsNINAnticipatedFlank()
			|| IsSAMAnticipatedFlank() || IsRPRAnticipatedFlank() || IsVPRAnticipatedFlank())
		{
			DrawSides();
		}
	}
}