using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components;
using BeatSaberMarkupLanguage.Parser;
using BeatSaberMarkupLanguage.ViewControllers;
using BetterSongSearch.UI.CustomLists;
using BetterSongSearch.Util;
using HMUI;
using IPA.Utilities;
using SongDetailsCache.Structs;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using static BetterSongSearch.UI.DownloadHistoryView;
using static BetterSongSearch.UI.SongSearchSong;

namespace BetterSongSearch.UI {
	[HotReload(RelativePathToLayout = @"Views\SongList.bsml")]
	[ViewDefinition("BetterSongSearch.UI.Views.SongList.bsml")]
	class SongListController : BSMLAutomaticViewController, TableView.IDataSource {
		public static PluginConfig cfgInstance;

		static internal IList<SongSearchSong> filteredSongsList = null;
		static internal IList<SongSearchSong> searchedSongsList = null;

		[UIComponent("searchInProgress")] readonly ImageView searchInProgress = null;

		[UIParams] readonly BSMLParserParams parserParams = null;

		public void ShowCloseConfirmation() => parserParams.EmitEvent("downloadCancelConfirm");
		[UIAction("ForcedUIClose")] void ForcedUIClose() => BSSFlowCoordinator.ConfirmCancelCallback(true);
		[UIAction("ForcedUICloseCancel")] void ForcedUICloseCancel() => BSSFlowCoordinator.ConfirmCancelCallback(false);


		RatelimitCoroutine limitedUpdateSearchedSongsList;
		public void UpdateSearchedSongsList() => StartCoroutine(limitedUpdateSearchedSongsList.CallNextFrame());

		public void _UpdateSearchedSongsList() {
			if(filteredSongsList == null)
				return;

			IPA.Utilities.Async.UnityMainThreadTaskScheduler.Factory.StartNew(() => searchInProgress.gameObject.SetActive(true));

			IEnumerable<SongSearchSong> _newSearchedSongsList;

			if(songSearchInput != null && songSearchInput.text.Length > 0) {
				_newSearchedSongsList = WeightedSongSearch.Search(filteredSongsList, songSearchInput.text, sortModes[selectedSortMode]);
			} else {
				_newSearchedSongsList = filteredSongsList.OrderByDescending(sortModes[selectedSortMode]);
			}

			if(songListData == null)
				return;

			var wasEmpty = searchedSongsList == null;

#if DEBUG
			var sw = new System.Diagnostics.Stopwatch();
			sw.Start();
#endif

			var i = 0;
			foreach(var song in _newSearchedSongsList)
				BSSFlowCoordinator.searchedSongsListPreallocatedArray[i++] = song;

#if DEBUG
			if(songSearchInput?.text.Length > 0)
				Plugin.Log.Info(string.Format("Searching the songs took {0}ms", sw.Elapsed.TotalMilliseconds));
#endif

			searchedSongsList = new ArraySegment<SongSearchSong>(BSSFlowCoordinator.searchedSongsListPreallocatedArray, 0, i);

			IPA.Utilities.Async.UnityMainThreadTaskScheduler.Factory.StartNew(() => {
				songList.ReloadData();

				if(BSSFlowCoordinator.songsList.Length == filteredSongsList.Count) {
					songSearchPlaceholder.text = $"Search by Song, Key, Mapper..";
				} else {
					songSearchPlaceholder.text = $"Search {searchedSongsList.Count} songs";
				}

				if(selectedSongView.selectedSong == null) {
					selectedSongView.SetSelectedSong(searchedSongsList.FirstOrDefault(), true);
				} else {
					if(wasEmpty) {
						//selectedSongView.SetSelectedSong(searchedSongsList.FirstOrDefault(x => x.detailsSong.mapId == selectedSongView.selectedSong.detailsSong.mapId), true);
						songList.ScrollToCellWithIdx(BSSFlowCoordinator.lastVisibleTableRowIdx, TableView.ScrollPositionType.Beginning, false);
					}
					// Always un-select in the list to prevent wrong-selections on resorting, etc.
					songList.ClearSelection();
				}


				searchInProgress.gameObject.SetActive(false);
			});
		}

		[UIAction("UpdateDataAndFilters")] void UpdateDataAndFilters(object _) => StartCoroutine(FilterView.limitedUpdateData.CallNextFrame());

		[UIAction("SelectRandom")]
		void SelectRandom() {
			if(searchedSongsList?.Count != 0)
				selectedSongView.SetSelectedSong(searchedSongsList[UnityEngine.Random.Range(0, searchedSongsList.Count - 1)], true);
		}



		internal SelectedSongView selectedSongView;

		void Awake() {
			selectedSongView = gameObject.AddComponent<SelectedSongView>();
			limitedUpdateSearchedSongsList = new RatelimitCoroutine(() => Task.Run(_UpdateSearchedSongsList), 0.1f);
		}

		[UIAction("SelectSong")] void _SelectSong(TableView _, int row) => selectedSongView.SetSelectedSong(searchedSongsList[row]);

		[UIComponent("songList")] public CustomListTableData songListData = null;
		public TableView songList => songListData?.tableView;
		[UIComponent("searchBoxContainer")] private VerticalLayoutGroup _searchBoxContainer = null;
		[UIComponent("scrollBarContainer")] private VerticalLayoutGroup _scrollBarContainer = null;

		internal InputFieldView songSearchInput { get; private set; } = null;
		CurvedTextMeshPro songSearchPlaceholder = null;

		[UIComponent("sortDropdown")] private DropdownWithTableView _sortDropdown = null;

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

			songList.SetDataSource(this, false);

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

			// Make the sort list BIGGER
			var c = Mathf.Min(9, _sortDropdown.tableViewDataSource.NumberOfCells());
			ReflectionUtil.SetField(_sortDropdown, "_numberOfVisibleCells", c);
			_sortDropdown.ReloadData();

			var m = ReflectionUtil.GetField<ModalView, DropdownWithTableView>(_sortDropdown, "_modalView");
			((RectTransform)m.transform).pivot = new Vector2(0.5f, 0.83f + (c * 0.011f));

			if(searchedSongsList == null)
				Task.Run(_UpdateSearchedSongsList);
		}

		public float CellSize() => PluginConfig.Instance.smallerFontSize ? 11.66f : 14f;
		public int NumberOfCells() => searchedSongsList?.Count ?? 0;
		public TableCell CellForIdx(TableView tableView, int idx) => SongListTableData.GetCell(tableView).PopulateWithSongData(searchedSongsList[idx]);

		BSMLParserParams multiDlParams = null;
		[UIAction("ShowMultiDlModal")]
		void ShowMultiDlModal() {
			BSMLStuff.InitSplitView(ref multiDlParams, gameObject, SplitViews.MultiDl.instance).EmitEvent("ShowModal");
		}

		BSMLParserParams createPlaylistParams = null;
		[UIAction("ShowPlaylistCreation")] void ShowPlaylistCreation() {
			BSMLStuff.InitSplitView(ref createPlaylistParams, gameObject, SplitViews.PlaylistCreation.instance);

			SplitViews.PlaylistCreation.instance.Open();
		}

		// While not the best for readability you have to agree this is a neat implementation!
		static readonly IReadOnlyDictionary<string, Func<SongSearchSong, float>> sortModes = new Dictionary<string, Func<SongSearchSong, float>>() {
			{ "Newest Upload", x => x.detailsSong.uploadTimeUnix },
			{ "Oldest Upload", x => uint.MaxValue - x.detailsSong.uploadTimeUnix },
			{ "Ranked/Qualified time", x => (x.detailsSong.rankedStatus != RankedStatus.Unranked ? x.detailsSong.rankedChangeUnix : 0f) },
			{ "Most Stars", x => x.diffs.Max(x => x.passesFilter && x.detailsDiff.ranked ? x.detailsDiff.stars : 0f) },
			{ "Least Stars", x => 420f - x.diffs.Min(x => x.passesFilter && x.detailsDiff.ranked ? x.detailsDiff.stars : 420f) },
			{ "Best rated", x => x.detailsSong.rating },
			{ "Worst rated", x => 420f - (x.detailsSong.rating != 0 ? x.detailsSong.rating : 420f) },
			//{ "Worst local score", x => {
			//	var returnVal = -420f;

			//	if(x.CheckHasScore()) {
			//		foreach(var diff in x.diffs) {
			//			var y = -sortModesDiffSort["Worst local score"](diff);

			//			if(y > returnVal)
			//				returnVal = y;
			//		}
			//	}

			//	return returnVal;
			//} }
		};

		internal static readonly IReadOnlyDictionary<string, Func<SongSearchDiff, float>> sortModesDiffSort = new Dictionary<string, Func<SongSearchDiff, float>>() {
			{ "Most Stars", x => -x.detailsDiff.stars },
			{ "Least Stars", x => x.detailsDiff.ranked ? x.detailsDiff.stars : -420f },
			//{ "Worst local score", x => {
			//	if(x.passesFilter && x.CheckHasScore())
			//		return x.localScore;

			//	return 420;
			//} }
		};

		static readonly IReadOnlyList<object> sortModeSelections = sortModes.Select(x => x.Key).ToList<object>();

		internal static string selectedSortMode { get; private set; } = sortModes.First().Key;
	}

	class SongSearchSong {
		const bool showVotesInsteadOfRating = true;

		public readonly Song detailsSong;

		string _hash = null;
		public string hash => _hash ??= detailsSong.hash;

		string _uploaderNameLowercase = null;
		public string uploaderNameLowercase => _uploaderNameLowercase ??= detailsSong.uploaderName.ToLowerInvariant();

		static unsafe string MakeStringSearchable(string s) {
			// Eh whatever
			if(s.Length > 255)
				return s;

			var normalizedString = s.Normalize(NormalizationForm.FormD);

			var pos = 0;
			var modified = false;
			char* challoc = stackalloc char[s.Length];

			for(var i = 0; i < normalizedString.Length; i++) {
				var c = normalizedString[i];

				var cat = CharUnicodeInfo.GetUnicodeCategory(c);

				if(cat == UnicodeCategory.NonSpacingMark) {
					modified = true;
					continue;
				}

				// adds 32 (Ascii ' ') to the A-Z charcode and thus converts it to a-z lmao
				if(cat == UnicodeCategory.LowercaseLetter || cat == UnicodeCategory.SpaceSeparator || cat == UnicodeCategory.DecimalDigitNumber) {
					challoc[pos++] = c;
				} else if(cat == UnicodeCategory.UppercaseLetter && c < '[') {
					challoc[pos++] = (char)(c + 32);
				}
			}

			if(!modified && pos == s.Length)
				return s;

			return new string(challoc, 0, pos);
		}


		string _searchableSongName = null;
		public string searchableSongName => _searchableSongName ??= MakeStringSearchable(detailsSong.songName);

		public readonly SongSearchDiff[] diffs;

		#region BSML stuffs
		public IOrderedEnumerable<SongSearchDiff> sortedDiffs {
			get {
				// Matching Standard > Matching Non-Standard > Non-Matching Standard > Non-Matching Non-Standard
				var y = diffs.OrderByDescending(x =>
					(x.passesFilter ? 1 : -3) + (x.detailsDiff.characteristic == MapCharacteristic.Standard ? 1 : 0)
				);

				// If we are sorting by something that is on a diff-level, sort the diffy as well!
				if(SongListController.sortModesDiffSort.TryGetValue(SongListController.selectedSortMode, out var diffSorter))
					y = y.ThenBy(diffSorter);

				return y.ThenByDescending(x => x.detailsDiff.ranked ? 1 : 0);
			}
		}

		public bool CheckIsDownloadedAndLoaded() => SongCore.Collections.songWithHashPresent(detailsSong.hash);

		public bool CheckIsDownloaded() {
			return
				BSSFlowCoordinator.downloadHistoryView.downloadList.Any(
					x => x.key == detailsSong.key &&
					x.status == DownloadHistoryEntry.DownloadStatus.Downloaded
				) || CheckIsDownloadedAndLoaded();
		}

		public bool CheckIsDownloadable() {
			var dlElem = BSSFlowCoordinator.downloadHistoryView.downloadList.FirstOrDefault(x => x.key == detailsSong.key);
			return dlElem == null || (
				(dlElem.retries == 3 && dlElem.status == DownloadHistoryEntry.DownloadStatus.Failed) ||
				(!dlElem.IsInAnyOfStates(DownloadHistoryEntry.DownloadStatus.Preparing | DownloadHistoryEntry.DownloadStatus.Downloading) && !CheckIsDownloaded())
			);
		}

		public bool CheckHasScore() => BSSFlowCoordinator.songsWithScores.ContainsKey(hash);

		bool isQualified => detailsSong.rankedStatus == RankedStatus.Qualified;

		public string fullFormattedSongName => $"{detailsSong.songAuthorName} - {detailsSong.songName}";
		public string uploadDateFormatted => detailsSong.uploadTime.ToString("dd. MMM yyyy", CultureInfo.InvariantCulture);
		public string songLength => string.Format("{0:00}:{1:00}", (int)detailsSong.songDuration.TotalMinutes, detailsSong.songDuration.Seconds);
		public string songRating => showVotesInsteadOfRating ? $"<color=#9C9>👍 {detailsSong.upvotes} <color=#C99>👎 {detailsSong.downvotes}" : $"{detailsSong.rating:0.0%}";

#if !DEBUG
		public string songLengthAndRating => $"{(isQualified ? "<color=#96C>🚩 Qualified</color> " : "")}⏲ {songLength}  {songRating}";
#else
		public float sortWeight = 0;
		public float resultWeight = 0;
		public string songLengthAndRating => $"RW{resultWeight} - SW{sortWeight} - {(isQualified ? "<color=#96C>🚩 Qualified</color> " : "")}⏲ {songLength}  {songRating}";
#endif
		//public string levelAuthorName => song.levelAuthorName;
		#endregion

		public string GetCustomLevelIdString() => $"custom_level_{detailsSong.hash.ToUpperInvariant()}";
		public SongSearchDiff GetFirstPassingDifficulty() => sortedDiffs.FirstOrDefault();
		public SongSearchSong(in Song song) {
			detailsSong = song;
			diffs = new SongSearchDiff[song.diffCount];

			// detailsSong.difficulties has an overhead of creating the ArraySegment - This doesnt 👍;
			for(var i = 0; i < diffs.Length; i++)
				diffs[i] = new SongSearchDiff(this, in BSSFlowCoordinator.songDetails.difficulties[i + (int)song.diffOffset]);
		}

		public class SongSearchDiff {
			internal readonly SongSearchSong songSearchSong;
			internal readonly SongDifficulty detailsDiff;
			internal bool? _passesFilter = null;
			internal bool passesFilter => _passesFilter ??= BSSFlowCoordinator.filterView.DifficultyCheck(in detailsDiff) && BSSFlowCoordinator.filterView.SearchDifficultyCheck(this);

			internal string serializedDiff => $"{detailsDiff.characteristic}_{detailsDiff.difficulty}";

			public bool CheckHasScore() => songSearchSong.CheckHasScore() && BSSFlowCoordinator.songsWithScores[songSearchSong.hash].ContainsKey(serializedDiff);
			internal float localScore => BSSFlowCoordinator.songsWithScores[songSearchSong.hash][serializedDiff];

			string GetCombinedShortDiffName() {
				var retVal = $"{(detailsDiff.song.diffCount > 5 ? shortMapDiffNames[detailsDiff.difficulty] : detailsDiff.difficulty.ToString())}";

				if(customCharNames.TryGetValue(detailsDiff.characteristic, out var customCharName))
					retVal += $"({customCharName})";

				return retVal;
			}
			public string formattedDiffDisplay => $"<color=#{(passesFilter ? "EEE" : "888")}>{GetCombinedShortDiffName()}</color>{(detailsDiff.ranked ? $" <color=#{(passesFilter ? "D91" : "650")}>{Math.Round(detailsDiff.stars, 1):0.0}⭐</color>" : "")}";
			public SongSearchDiff(SongSearchSong songSearchSong, in SongDifficulty diff) {
				this.detailsDiff = diff;
				this.songSearchSong = songSearchSong;
			}

			static readonly IReadOnlyDictionary<MapDifficulty, string> shortMapDiffNames = new Dictionary<MapDifficulty, string> {
				{ MapDifficulty.Easy, "Easy" },
				{ MapDifficulty.Normal, "Norm" },
				{ MapDifficulty.Hard, "Hard" },
				{ MapDifficulty.Expert, "Ex" },
				{ MapDifficulty.ExpertPlus, "Ex+" }
			};

			static readonly IReadOnlyDictionary<MapCharacteristic, string> customCharNames = new Dictionary<MapCharacteristic, string> {
				{ MapCharacteristic.NinetyDegree, "90" },
				{ MapCharacteristic.ThreeSixtyDegree, "360" },
				{ MapCharacteristic.Lawless, "☠" },
				{ MapCharacteristic.Custom, "?" },
				{ MapCharacteristic.Lightshow, "💡" }
			};
		}
	}
}
