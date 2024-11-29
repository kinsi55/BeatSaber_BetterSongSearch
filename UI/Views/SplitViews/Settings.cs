using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components.Settings;
using BeatSaberMarkupLanguage.Parser;

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
