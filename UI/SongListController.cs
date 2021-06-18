using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components;
using BeatSaberMarkupLanguage.Parser;
using BeatSaberMarkupLanguage.ViewControllers;
using BetterSongSearch.Util;
using HMUI;
using IPA.Utilities;
using SongDetailsCache.Structs;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BetterSongSearch.UI {
	[HotReload(RelativePathToLayout = @"Views\SongList.bsml")]
	[ViewDefinition("BetterSongSearch.UI.Views.SongList.bsml")]
	class SongListController : BSMLAutomaticViewController {
		static internal IEnumerable<SongSearchSong> filteredSongsList = new List<SongSearchSong>();
		static internal List<object> searchedSongsList = new List<object>();

		[UIComponent("searchInProgress")] readonly ImageView searchInProgress = null;

		[UIParams] readonly BSMLParserParams parserParams = null;

		public void ShowCloseConfirmation() => parserParams.EmitEvent("downloadCancelConfirm");
		[UIAction("ForcedUIClose")] void ForcedUIClose() => BSSFlowCoordinator.Close(false, false);


		RatelimitCoroutine limitedUpdateSearchedSongsList;
		public void UpdateSearchedSongsList() => StartCoroutine(limitedUpdateSearchedSongsList.CallNextFrame());

		public void _UpdateSearchedSongsList() {
			IPA.Utilities.Async.UnityMainThreadTaskScheduler.Factory.StartNew(() => searchInProgress.gameObject.SetActive(true));

			IEnumerable<SongSearchSong> _newSearchedSongsList;

			if(songSearchInput != null && songSearchInput.text.Length > 0) {
				_newSearchedSongsList = WeightedSongSearch.Search(filteredSongsList, songSearchInput.text, sortModes[opt_sort]);
			} else {
				_newSearchedSongsList = filteredSongsList.OrderByDescending(sortModes[opt_sort]);
			}

			var newSearchedSongsList = _newSearchedSongsList.Cast<object>().ToList();

			var songToSelect = (SongSearchSong)newSearchedSongsList.FirstOrDefault(x => ((SongSearchSong)x).detailsSong.mapId == selectedSongView.selectedSong?.detailsSong.mapId);

			if(newSearchedSongsList.Count > 0 && songToSelect == null)
				songToSelect = (SongSearchSong)newSearchedSongsList[0];

			if(songListData == null)
				return;

			IPA.Utilities.Async.UnityMainThreadTaskScheduler.Factory.StartNew(() => {
				songListData.data = newSearchedSongsList;
				searchedSongsList = newSearchedSongsList;

				StartCoroutine(_AntiLagRefreshTable(true));

				songSearchPlaceholder.text = $"Search {searchedSongsList.Count()} songs";

				selectedSongView.SetSelectedSong(songToSelect, false);

				// Required as otherwise the first cell could be selected eventho its not
				songList.ClearSelection();

				searchInProgress.gameObject.SetActive(false);
			});
		}

		public void RefreshTable() => StartCoroutine(_AntiLagRefreshTable());

		// This will spawn the UI rows in the table in batches so its not one big lag spike but 2/3 smaller ones
		IEnumerator _AntiLagRefreshTable(bool reload = false) {
			var x = (songList.transform as RectTransform);

			void setH(float h) {
				var copi = x.offsetMin;
				copi.y = h;

				x.offsetMin = copi;
			}

			setH(11.7f * 4);
			var s = new Stopwatch();
			s.Start();

			if(reload) {
				BSMLStuff.UnleakTable(songList.gameObject);
				songList.ReloadData();
			} else {
				songList.RefreshCells(false, true);
			}

			var gap = Math.Max(0.1f, (s.ElapsedMilliseconds / 1000f) * 3);

			yield return new WaitForSeconds(gap);
			setH(11.7f * 2);
			songList.RefreshCells(false, false);

			yield return new WaitForSeconds(gap);
			setH(0);
			songList.RefreshCells(false, false);
		}

		[UIAction("UpdateData")] void UpdateData(object _) => UpdateSearchedSongsList();

		[UIAction("SelectRandom")] void SelectRandom() {
			if(searchedSongsList.Count > 0)
				selectedSongView.SetSelectedSong((SongSearchSong)searchedSongsList[UnityEngine.Random.Range(0, searchedSongsList.Count - 1)], true);
		}

		internal SelectedSongView selectedSongView;

		void Awake() {
			searchedSongsList ??= new List<object>();
			selectedSongView = gameObject.AddComponent<SelectedSongView>();
			limitedUpdateSearchedSongsList = new RatelimitCoroutine(() => Task.Run(_UpdateSearchedSongsList), 0.5f);
		}

		[UIAction("SelectSong")] void _SelectSong(TableView _, SongSearchSong row) => selectedSongView.SetSelectedSong(row);

		[UIComponent("songList")] public CustomCellListTableData songListData = null;
		public TableView songList => songListData?.tableView; 
		[UIComponent("searchBoxContainer")] private VerticalLayoutGroup _searchBoxContainer = null;
		[UIComponent("scrollBarContainer")] private VerticalLayoutGroup _scrollBarContainer = null;

		InputFieldView songSearchInput = null;
		CurvedTextMeshPro songSearchPlaceholder = null;

		[UIAction("#post-parse")]
		void Parsed() {
			// Yoink the basegame song filter box for this
			var searchBox = Resources.FindObjectsOfTypeAll<InputFieldView>().FirstOrDefault(x => x.gameObject.name == "SearchInputField")?.gameObject;

			if(searchBox != null) {
				var newSearchBox = Instantiate(searchBox, _searchBoxContainer.transform, false);
				songSearchInput = newSearchBox.GetComponent<InputFieldView>();
				songSearchPlaceholder = newSearchBox.transform.Find("PlaceholderText")?.GetComponent<CurvedTextMeshPro>();

				ReflectionUtil.SetField(songSearchInput, "_keyboardPositionOffset", new Vector3(-15, -36));

				songSearchInput.onValueChanged.AddListener(_ => UpdateSearchedSongsList());
			}

			BSMLStuff.GetScrollbarForTable(songListData.gameObject, _scrollBarContainer.transform);

			// Funny bsml bug where scrolling would not work otherwise
			IVRPlatformHelper meWhen = null;
			foreach(var x in Resources.FindObjectsOfTypeAll<ScrollView>()) {
				meWhen = ReflectionUtil.GetField<IVRPlatformHelper, ScrollView>(x, "_platformHelper");
				if(meWhen != null)
					break;
			}

			//foreach(var g in new MonoBehaviour[] { filterView, songListView, downloadHistoryView })
			foreach(var x in GetComponentsInChildren<ScrollView>()) ReflectionUtil.SetField(x, "_platformHelper", meWhen);
		}

		// While not the best for readability you have to agree this is a neat implementation!
		static readonly IReadOnlyDictionary<string, Func<SongSearchSong, float>> sortModes = new Dictionary<string, Func<SongSearchSong, float>>() {
			{ "New first", x => x.detailsSong.uploadTimeUnix },
			{ "Old first", x => uint.MaxValue - x.detailsSong.uploadTimeUnix },
			{ "Most Stars first", x => x.diffs.Max(x => x.passesFilter && x.detailsDiff.ranked ? x.detailsDiff.stars : 0f) },
			{ "Best rated first", x => x.detailsSong.rating },
			{ "Most Downloads first", x => x.detailsSong.downloadCount },
			{ "Worst rated first", x => 420f - (x.detailsSong.rating != 0 ? x.detailsSong.rating : 420f) },
			{ "Least Stars first", x => 420f - x.diffs.Min(x => x.passesFilter && x.detailsDiff.ranked ? x.detailsDiff.stars : 420f) }
		};

		static readonly IReadOnlyList<object> sortModeSelections = sortModes.Select(x => x.Key).Cast<object>().ToList();

		static string opt_sort = sortModes.First().Key;
	}

	public class SongSearchSong {
		const bool showVotesInsteadOfRating = true;

		public readonly Song detailsSong;
		public SongSearchDiff[] diffs { get; private set; }
		public SongSearchDiff[] _sortedDiffsCache;

		#region BSML stuffs
		// This makes sure to always have a |ranked matching > standard matching > ranked unmatching > standard unmatching > everything else| sort for the difficulties!
		public SongSearchDiff[] sortedDiffs => _sortedDiffsCache ??= diffs.OrderByDescending(x =>
			(x.passesFilter ? 1 : -3) + (x.detailsDiff.characteristic == MapCharacteristic.Standard ? 1 : 0) + (x.detailsDiff.ranked ? 2 : 0)
		).ToArray();

		public bool CheckIsDownloadedAndLoaded() => SongCore.Collections.songWithHashPresent(detailsSong.hash);

		public bool CheckIsDownloaded() {
			if(BSSFlowCoordinator.downloadHistoryView.downloadList.FirstOrDefault(x => x.key == detailsSong.key)?.status == DownloadHistoryView.Entry.DownloadStatus.Downloaded)
				return true;

			return CheckIsDownloadedAndLoaded();
		}

		public bool CheckIsDownloadable() {
			var dlElem = BSSFlowCoordinator.downloadHistoryView.downloadList.FirstOrDefault(x => x.key == detailsSong.key);
			return dlElem == null || (
				(dlElem.retries == 3 && dlElem.status == DownloadHistoryView.Entry.DownloadStatus.Failed) ||
				(dlElem.status != DownloadHistoryView.Entry.DownloadStatus.Downloading && !CheckIsDownloaded())
			);
		}

		public string fullFormattedSongName => $"<color=#{(CheckIsDownloaded() ? "888" : "FFF")}>{detailsSong.songAuthorName} - {detailsSong.songName}</color>";
		public string uploadDateFormatted => detailsSong.uploadTime.ToString(" dd. MMM yyyy", new CultureInfo("en-US"));
		public string songLength => detailsSong.songDuration.ToString("mm\\:ss");
		public string songRating => showVotesInsteadOfRating ? $"👍 {detailsSong.upvotes} 👎 {detailsSong.downvotes}" : $"{detailsSong.rating:0.0%}";

		public string songLengthAndRating => $"⏲ {songLength}  {songRating}";
		//public string levelAuthorName => song.levelAuthorName;
		#endregion

		public string GetCustomLevelIdString() => $"custom_level_{detailsSong.hash.ToUpper()}";
		public SongSearchDiff GetFirstPassingDifficulty() {
			return diffs.OrderByDescending(x => (x.passesFilter ? 2 : 0) + (x.detailsDiff.characteristic == MapCharacteristic.Standard ? 1 : -1)).First();
		}
		public SongSearchSong(in Song song) {
			detailsSong = song;
			diffs = new SongSearchDiff[song.diffCount];

			// detailsSong.difficulties has an overhead of creating the ArraySegment - This doesnt 👍;
			for(int i = 0; i < diffs.Length; i++)
				diffs[i] = new SongSearchDiff(in BSSFlowCoordinator.songDetails.difficulties[i + (int)song.diffOffset]);
		}

		public class SongSearchDiff {
			internal readonly SongDifficulty detailsDiff;
			internal bool? _passesFilter = null;
			internal bool passesFilter => _passesFilter ??= BSSFlowCoordinator.filterView.DifficultyCheck(in detailsDiff);

			string GetCombinedShortDiffName() {
				string retVal = $"{(detailsDiff.song.diffCount > 5 ? shortMapDiffNames[detailsDiff.difficulty] : detailsDiff.difficulty.ToString())}";

				if(customCharNames.ContainsKey(detailsDiff.characteristic))
					retVal += $"({customCharNames[detailsDiff.characteristic]})";

				return retVal;
			}
			string formattedDiffDisplay => $"<color=#{(passesFilter ? "EEE" : "888")}>{GetCombinedShortDiffName()}</color>{(detailsDiff.ranked ? $" <color=#{(passesFilter ? "D91" : "650")}>{Math.Round(detailsDiff.stars, 1):0.0}⭐</color>" : "")}";
			public SongSearchDiff(in SongDifficulty diff) {
				this.detailsDiff = diff;
			}

			static readonly Dictionary<MapDifficulty, string> shortMapDiffNames = new Dictionary<MapDifficulty, string> {
				{ MapDifficulty.Easy, "E" },
				{ MapDifficulty.Normal, "N" },
				{ MapDifficulty.Hard, "H" },
				{ MapDifficulty.Expert, "Ex" },
				{ MapDifficulty.ExpertPlus, "E+" }
			};

			static readonly Dictionary<MapCharacteristic, string> customCharNames = new Dictionary<MapCharacteristic, string> {
				{ MapCharacteristic.NinetyDegree, "90" },
				{ MapCharacteristic.ThreeSixtyDegree, "360" },
				{ MapCharacteristic.Lawless, "☠" },
				{ MapCharacteristic.Custom, "?" },
				{ MapCharacteristic.Lightshow, "💡" }
			};
		}


		[UIComponent("bgContainer")] ImageView bg = null;
		[UIAction("refresh-visuals")]
		public void Refresh(bool selected, bool highlighted) {
			bg.color = new Color(0, 0, 0, selected ? 0.9f : highlighted ? 0.6f : 0.45f);
		}
	}
}
