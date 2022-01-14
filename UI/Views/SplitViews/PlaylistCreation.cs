using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components.Settings;
using BeatSaberMarkupLanguage.Parser;
using IPA.Utilities;
using TMPro;
using BeatSaberPlaylistsLib;
using BeatSaberPlaylistsLib.Types;
using System.IO;

namespace BetterSongSearch.UI.SplitViews {
	class PlaylistCreation {
		public static readonly PlaylistCreation instance = new PlaylistCreation();
		PlaylistCreation() { }

		[UIComponent("playlistSongsCountSlider")] SliderSetting playlistSongsCountSlider = null;
		[UIComponent("playlistName")] StringSetting playlistName = null;
		[UIComponent("resultText")] TextMeshProUGUI resultText = null;

		[UIParams] readonly BSMLParserParams parserParams = null;

		[UIAction("#post-parse")]
		void Parsed() {
			// BSML / HMUI my beloved
			ReflectionUtil.SetField(playlistName.modalKeyboard.modalView, "_animateParentCanvas", false);
		}

		public void Open() {
			if(IPA.Loader.PluginManager.GetPluginFromId("BeatSaberPlaylistsLib") == null) {
				resultText.text = "You dont have 'BeatSaberPlaylistsLib' installed which is required to create Playlists. You can get it in ModAssistant.";
				parserParams.EmitEvent("ShowResultModal");
				return;
			}

			parserParams.EmitEvent("ShowModal");

			playlistName.Text = "";
		}

		void CreatePlaylist() {
			if(!PlaylistManager.DefaultManager.TryGetPlaylist(playlistName.Text, out var plist))
				plist = PlaylistManager.DefaultManager.CreatePlaylist(
					$"BetterSongSearch - {string.Concat(playlistName.Text.Split(Path.GetInvalidFileNameChars())).Trim()}", 
					playlistName.Text, 
					"BetterSongSearch",
					""
				);

			plist.Clear();
			plist.SetCustomData("BetterSongSearchFilter", FilterView.currentFilter.Serialize(Newtonsoft.Json.Formatting.None));
			plist.SetCustomData("BetterSongSearchSearchTerm", BSSFlowCoordinator.songListView.songSearchInput.text);

			for(var i = 0; i < SongListController.searchedSongsList.Count; i++) {
				if(i >= playlistSongsCountSlider.Value)
					break;

				var s = SongListController.searchedSongsList[i];

				var pls = (PlaylistSong)plist.Add(s.hash, s.detailsSong.songName, s.detailsSong.key, s.detailsSong.levelAuthorName);

				foreach(var x in s.diffs) {
					if(!x.passesFilter)
						continue;

					var dchar = x.detailsDiff.characteristic.ToString();

					if(dchar == "ThreeSixtyDegree")
						dchar = "Degree360";

					if(dchar == "NinetyDegree")
						dchar = "Degree90";

					pls.AddDifficulty(dchar, x.detailsDiff.difficulty.ToString());
				}
			}

			PlaylistManager.DefaultManager.StorePlaylist(plist);
			PlaylistManager.DefaultManager.RequestRefresh("BetterSongSearch");

			resultText.text = $"Created Playlist <b><color=#CCC>{playlistName.Text}</color></b> containing <b><color=#CCC>{plist.Count}</color></b> Songs";
			parserParams.EmitEvent("ShowResultModal");
		}
	}
}
