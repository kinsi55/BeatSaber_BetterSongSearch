using BetterSongSearch.UI;
using HarmonyLib;
using HMUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BetterSongSearch.HarmonyPatches {
	[HarmonyPatch(typeof(FlowCoordinator), "DismissFlowCoordinator")]
	static class ReturnToBSS {
		public static bool returnTobss = false;
		static void Prefix(FlowCoordinator flowCoordinator, ref bool immediately) {
			if(!returnTobss)
				return;

			if(!(flowCoordinator is SoloFreePlayFlowCoordinator))
				return;

			immediately = true;
		}

		static void Postfix() {
			if(!returnTobss)
				return;

			returnTobss = false;

			Manager.ShowFlow(true);
		}
	}
}
