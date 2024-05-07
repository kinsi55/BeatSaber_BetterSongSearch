using BetterSongSearch.UI;
using HarmonyLib;
using HMUI;
using System.Linq;
using UnityEngine;

namespace BetterSongSearch.HarmonyPatches {
	[HarmonyPatch(typeof(GameplaySetupViewController), nameof(GameplaySetupViewController.RefreshContent))]
	static class MPMenuButton {
		static GameObject button = null;
		static void Postfix(GameplaySetupViewController __instance) {
			// I dont want the plugin to ever break because of changes to this
			try {
				if(button == null) {
					var x = __instance.transform.Find("BSMLBackground/BSMLTabSelector") ?? __instance.transform.Find("TextSegmentedControl");

					if(x == null)
						return;

					button = GameObject.Instantiate(x.Cast<Transform>().Last().gameObject, x);

					var t = button.GetComponent<TextSegmentedControlCell>();

					t.text = "Better Song Search";
					t._wasPressedSignal = null;
					t.selectionDidChangeEvent += (A, B, CBADQ) => {
						if(!t.selected)
							return;

						t.SetSelected(false, SelectableCell.TransitionType.Instant, false, true);
						Manager.ShowFlow();
					};
				}

				button.SetActive(__instance._showMultiplayer);
			} catch { }
		}
	}
}
