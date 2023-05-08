using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components.Settings;
using BeatSaberMarkupLanguage.Parser;
using UnityEngine;

namespace BetterSongSearch.UI.SplitViews {
	class Settings {
		public static readonly Settings instance = new Settings();
		Settings() { }

		static readonly IReadOnlyList<object> preferredLeaderboardChoices = new List<object>() { "ScoreSaber", "BeatLeader" };

		string preferredLeaderboard {
			get => PluginConfig.Instance.preferredLeaderboard;
			set {
				PluginConfig.Instance.preferredLeaderboard = value;

				FilterView.limitedUpdateData.CallNextFrame();
			}
		}


		[UIParams] readonly BSMLParserParams parserParams = null;

		public static PluginConfig cfgInstance;

		[UIComponent("chinaToggle")] readonly ToggleSetting chinaToggle = null;

		bool xina {
			get => cfgInstance.downloadUrlOverride.StartsWith("https://beatsaver.wgzeyu.vip/sea", System.StringComparison.OrdinalIgnoreCase);
			set {
				if(!xina && !value)
					return;

				if(value) {
					cfgInstance.apiUrlOverride = "https://beatsaver.wgzeyu.vip/api/maps/id";
					cfgInstance.downloadUrlOverride = "https://beatsaver.wgzeyu.vip/sea";
					cfgInstance.coverUrlOverride = "https://beatsaver.wgzeyu.vip/cdn";
					cfgInstance.previewUrlOverride = "https://beatsaver.wgzeyu.vip/cdn";
				} else {
					cfgInstance.apiUrlOverride = 
						cfgInstance.downloadUrlOverride =
						cfgInstance.coverUrlOverride =
						cfgInstance.previewUrlOverride = "";
				}
			}
		}


		[UIAction("ChinaToggled")]
		void ChinaToggled(bool val) {
			if(!val) {
				xina = false;
			} else {
				// Needs to be delayed by a frame because otherwise some times the toggle wont turn off
				SharedCoroutineStarter.instance.StartCoroutine(ShowChinaModal());
			}
		}

		IEnumerator ShowChinaModal() {
			yield return null;
			chinaToggle.Value = false;
			chinaToggle.ReceiveValue();
			parserParams.EmitEvent("CloseSettingsModal");
			parserParams.EmitEvent("ShowChinaModal");
		}

		[UIAction("EnableChinaMirror")]
		void EnableChinaMirror() {
			xina = true;

			chinaToggle.Value = true;
			parserParams.EmitEvent("ShowModal");
			chinaToggle.ReceiveValue();
		}

		[UIAction("ShowWGzeyuSite")]
		void ShowWGzeyuSite() => Process.Start("https://bs.wgzeyu.com/pc-guide");

		[UIAction("#post-parse")]
		void Parsed() {
			chinaToggle.Value = xina;
			chinaToggle.ReceiveValue();
		}
	}
}
