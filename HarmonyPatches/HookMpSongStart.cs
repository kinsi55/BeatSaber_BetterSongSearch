using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BetterSongSearch.UI;
using HarmonyLib;

namespace BetterSongSearch.HarmonyPatches {
	[HarmonyPatch(typeof(MultiplayerLevelScenesTransitionSetupDataSO), nameof(MultiplayerLevelScenesTransitionSetupDataSO.Init))]
	static class HookMpSongStart {
		static void Prefix() => BSSFlowCoordinator.Close(true, false);
	}
}
