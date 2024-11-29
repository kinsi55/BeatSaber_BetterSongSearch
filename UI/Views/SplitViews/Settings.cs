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
	}
}
