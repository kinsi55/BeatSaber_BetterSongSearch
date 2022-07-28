using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components.Settings;
using BeatSaberMarkupLanguage.Parser;
using BeatSaberPlaylistsLib;
using BeatSaberPlaylistsLib.Types;
using IPA.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;

namespace BetterSongSearch.UI.SplitViews {
	class PlaylistCreation {
		public static readonly PlaylistCreation instance = new PlaylistCreation();
		PlaylistCreation() { }

		[UIComponent("playlistSongsCountSlider")] readonly SliderSetting playlistSongsCountSlider = null;
		[UIComponent("playlistName")] readonly StringSetting playlistName = null;
		[UIComponent("resultText")] readonly TextMeshProUGUI resultText = null;

		[UIParams] readonly BSMLParserParams parserParams = null;

		[UIAction("#post-parse")]
		void Parsed() {
			ReflectionUtil.SetField(playlistName.modalKeyboard.modalView, "_animateParentCanvas", false);
		}

		internal static string nameToUseOnNextOpen = "h";
		static bool clearExisting = true;
		static bool hightlightDiffs = false;

		public void Open() {
			if(IPA.Loader.PluginManager.GetPluginFromId("BeatSaberPlaylistsLib") == null) {
				resultText.text = "You dont have 'BeatSaberPlaylistsLib' installed which is required to create Playlists. You can get it in ModAssistant.";
				parserParams.EmitEvent("ShowResultModal");
				return;
			}

			if(nameToUseOnNextOpen != null) {
				playlistName.Text = nameToUseOnNextOpen;
				nameToUseOnNextOpen = null;
			}

			parserParams.EmitEvent("ShowModal");
		}

		void ShowResult(string text) {
			resultText.text = text;
			parserParams.EmitEvent("CloseModal");
			parserParams.EmitEvent("ShowResultModal");
		}

		static readonly IReadOnlyDictionary<SongDetailsCache.Structs.MapCharacteristic, string> songDetailsCharNames
			= Enum.GetValues(typeof(SongDetailsCache.Structs.MapCharacteristic))
			.Cast<SongDetailsCache.Structs.MapCharacteristic>()
			.ToDictionary(x => x, x => x.ToString());

		static readonly IReadOnlyDictionary<SongDetailsCache.Structs.MapDifficulty, string> songDetailsDiffNames
			= Enum.GetValues(typeof(SongDetailsCache.Structs.MapDifficulty))
			.Cast<SongDetailsCache.Structs.MapDifficulty>()
			.ToDictionary(x => x, x => x.ToString());

		void CreatePlaylist() {
			var fName = string.Concat(playlistName.Text.Split(Path.GetInvalidFileNameChars())).Trim();

			if(fName.Length == 0) {
				ShowResult($"Your Playlist name is invalid");
				return;
			}

			try {
				var manager = PlaylistManager.DefaultManager.CreateChildManager("BetterSongSearch");

				if(!manager.TryGetPlaylist(fName, out var plist))
					plist = manager.CreatePlaylist(
						fName,
						playlistName.Text,
						"BetterSongSearch",
						""
					);

				if(clearExisting)
					plist.Clear();

				plist.SetCustomData("BetterSongSearchFilter", FilterView.currentFilter.Serialize(Newtonsoft.Json.Formatting.None));
				plist.SetCustomData("BetterSongSearchSearchTerm", BSSFlowCoordinator.songListView.songSearchInput.text);
				plist.SetCustomData("BetterSongSearchSort", SongListController.selectedSortMode);
				// PlaylistLib duplicate check is O(n^2) - Not gud enough for batch-adding with thousands of entries, so we roll out own
				plist.AllowDuplicates = true;

				// PlaylistLib contains uppercase hashes, but in that case I dont have control over it and dont know if it might change
				var songsAlreadyInPlaylist = plist.Select(x => x.Hash.ToUpperInvariant()).ToHashSet();

				int addedSongs = 0;

				for(var i = 0; i < SongListController.searchedSongsList.Count; i++) {
					if(addedSongs >= playlistSongsCountSlider.Value)
						break;

					var s = SongListController.searchedSongsList[i];

					PlaylistSong pls = null;
					// SongDetails returns uppercase hashes
					var uH = s.hash;

					if(!songsAlreadyInPlaylist.Contains(uH))
						pls = (PlaylistSong)plist.Add(uH, s.detailsSong.songName, s.detailsSong.key, s.detailsSong.levelAuthorName);

					if(pls == null)
						continue;

					songsAlreadyInPlaylist.Add(uH);

					addedSongs++;

					if(!hightlightDiffs)
						continue;

					foreach(var x in s.diffs) {
						if(!x.passesFilter)
							continue;

						pls.AddDifficulty(
							songDetailsCharNames[x.detailsDiff.characteristic],
							songDetailsDiffNames[x.detailsDiff.difficulty]
						);
					}
				}

				plist.AllowDuplicates = false;
				manager.StorePlaylist(plist);
				manager.RequestRefresh("BetterSongSearch");

				ShowResult($"Added <b><color=#CCC>{addedSongs}</color></b> Songs to Playlist <b><color=#CCC>{playlistName.Text}</color></b> (Contains {plist.Count} now)");
			} catch(Exception ex) {
				ShowResult($"Playlist failed to Create: More details in log, {ex.GetType().Name}");
				Plugin.Log.Warn("Failed to create Playlist:");
				Plugin.Log.Warn(ex);
			}
		}
	}
}
