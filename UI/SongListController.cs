using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components;
using BeatSaberMarkupLanguage.Parser;
using BeatSaberMarkupLanguage.ViewControllers;
using BetterSongSearch.UI.CustomLists;
using BetterSongSearch.Util;
using HMUI;
using IPA.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using static BetterSongSearch.Util.SongSearchSong;

namespace BetterSongSearch.UI {
	[HotReload(RelativePathToLayout = @"Views\SongList.bsml")]
	[ViewDefinition("BetterSongSearch.UI.Views.SongList.bsml")]
	class SongListController : BSMLAutomaticViewController, TableView.IDataSource {
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

				songSearchInput._keyboardPositionOffset = new Vector3(-15, -36);

				songSearchInput.onValueChanged.AddListener(_ => UpdateSearchedSongsList());
			}

			songList.SetDataSource(this, false);

			BSMLStuff.GetScrollbarForTable(songListData.gameObject, _scrollBarContainer.transform);

			// Funny bsml bug where scrolling would not work otherwise
			IVRPlatformHelper meWhen = null;
			foreach(var x in Resources.FindObjectsOfTypeAll<ScrollView>()) {
				meWhen = x._platformHelper;
				if(meWhen != null)
					break;
			}

			//foreach(var g in new MonoBehaviour[] { filterView, songListView, downloadHistoryView })
			foreach(var x in GetComponentsInChildren<ScrollView>())
				ReflectionUtil.SetField(x, nameof(x._platformHelper), meWhen);

			// Make the sort list BIGGER
			var c = Mathf.Min(9, _sortDropdown.tableViewDataSource.NumberOfCells());
			_sortDropdown._numberOfVisibleCells = c;
			_sortDropdown.ReloadData();

			var m = _sortDropdown._modalView;
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

		BSMLParserParams settingsParams = null;
		[UIAction("ShowSettings")]
		void ShowSettings() {
			BSMLStuff.InitSplitView(ref settingsParams, gameObject, SplitViews.Settings.instance).EmitEvent("ShowModal");
		}

		// While not the best for readability you have to agree this is a neat implementation!
		static readonly IReadOnlyDictionary<string, Func<SongSearchSong, float>> sortModes = new Dictionary<string, Func<SongSearchSong, float>>() {
			{ "Newest Upload", x => x.detailsSong.uploadTimeUnix },
			{ "Oldest Upload", x => uint.MaxValue - x.detailsSong.uploadTimeUnix },
			{ "Ranked/Qualified time", x => (x.isRanked() ? x.detailsSong.rankedChangeUnix : 0f) },
			{ "Most Stars", x => x.diffs.Max(x => x.passesFilter && x.songSearchSong.isRanked() ? x.GetStars() : 0f) },
			{ "Least Stars", x => 420f - x.diffs.Min(x => x.passesFilter && x.songSearchSong.isRanked() ? x.GetStars() : 420f) },
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
			{ "Most Stars", x => -x.GetStars() },
			{ "Least Stars", x => x.songSearchSong.isRanked() ? x.GetStars() : -420f },
			//{ "Worst local score", x => {
			//	if(x.passesFilter && x.CheckHasScore())
			//		return x.localScore;

			//	return 420;
			//} }
		};

		static readonly IReadOnlyList<object> sortModeSelections = sortModes.Select(x => x.Key).ToList<object>();

		internal static string selectedSortMode { get; private set; } = sortModes.First().Key;
	}
}
