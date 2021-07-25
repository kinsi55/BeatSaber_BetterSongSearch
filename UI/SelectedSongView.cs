using BeatSaberMarkupLanguage.Attributes;
using BetterSongSearch.HarmonyPatches;
using BetterSongSearch.Util;
using HMUI;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static SelectLevelCategoryViewController;

namespace BetterSongSearch.UI {
	class SelectedSongView : MonoBehaviour {
		internal SongSearchSong selectedSong = null;

		[UIComponent("coverLoading")] ImageView coverLoading = null;
		[UIComponent("coverImage")] Image coverImage = null;
		[UIComponent("selectedSongAuthor")] TextMeshProUGUI selectedSongAuthor = null;
		[UIComponent("selectedSongName")] TextMeshProUGUI selectedSongName = null;
		[UIComponent("selectedSongDiffInfo")] TextMeshProUGUI selectedSongDiffInfo = null;

		[UIComponent("downloadButton")] NoTransitionsButton downloadButton = null;
		[UIComponent("playButton")] NoTransitionsButton playButton = null;
		[UIComponent("songDetailsButton")] NoTransitionsButton songDetailsButton = null;

		static Material lol = null;
		[UIAction("#post-parse")]
		void Parsed() {
			coverImage.material = XD.FunnyMono(lol) ?? (lol = Resources.FindObjectsOfTypeAll<Material>().FirstOrDefault(m => m.name == "UINoGlowRoundEdge"));
		}

		void ShowCoverLoader(bool show) {
			if(show)
				coverImage.sprite = SongCore.Loader.defaultCoverImage;
			coverLoading.gameObject.SetActive(show);
		}

		static internal SongPreviewPlayer songPreviewPlayer { get; private set; } = null;
		static BeatmapLevelsModel _beatmapLevelsModel = null;
		static BeatmapLevelsModel beatmapLevelsModel => XD.FunnyMono(_beatmapLevelsModel) ?? (_beatmapLevelsModel = Resources.FindObjectsOfTypeAll<BeatmapLevelsModel>().FirstOrDefault(x => x.customLevelPackCollection != null));

		static LevelCollectionViewController _levelCollectionViewController;
		static LevelCollectionViewController levelCollectionViewController => XD.FunnyMono(_levelCollectionViewController) ?? (_levelCollectionViewController = Resources.FindObjectsOfTypeAll<LevelCollectionViewController>().FirstOrDefault());


		static internal CancellationTokenSource coverLoadCancel { get; private set; } = null;
		internal async void SetSelectedSong(SongSearchSong song, bool selectInTableIfPossible = false) {
			if(song == null || songToPlayAfterLoading != null)
				return;

			var prevKey = selectedSong?.detailsSong.mapId;

			selectedSong = song;

			SetIsDownloaded(song.CheckIsDownloaded(), song.CheckIsDownloadable());

			selectedSongAuthor.text = song.detailsSong.songAuthorName;
			selectedSongName.text = song.detailsSong.songName;

			songDetailsButton.gameObject.SetActive(true);

			if(song.diffs.Length > 1) {
				selectedSongDiffInfo.text = string.Format(
					"{0:0.00} - {1:0.00} NPS | {2:0.00} - {3:0.00} NJS",
					(float)song.diffs.Min(x => x.detailsDiff.notes) / song.detailsSong.songDurationSeconds,
					(float)song.diffs.Max(x => x.detailsDiff.notes) / song.detailsSong.songDurationSeconds,
					song.diffs.Min(x => x.detailsDiff.njs),
					song.diffs.Max(x => x.detailsDiff.njs)
				);
			} else if(song.diffs.Length > 0) {
				selectedSongDiffInfo.text = string.Format(
					"{0:0.00} NPS | {1:0.00} NJS",
					(float)song.diffs[0].detailsDiff.notes / song.detailsSong.songDurationSeconds,
					song.diffs[0].detailsDiff.njs
				);
			}

			if(selectInTableIfPossible) {
				var idx = SongListController.searchedSongsList.IndexOf(song);
				var tb = BSSFlowCoordinator.songListView.songList;

				if(idx != -1) {
					tb.ScrollToCellWithIdx(idx, TableView.ScrollPositionType.Center, false);
					tb.SelectCellWithIdx(idx);
				} else {
					tb.ClearSelection();
				}
			}

			if(prevKey == selectedSong.detailsSong.mapId)
				return;

			ShowCoverLoader(true);

			coverLoadCancel?.Cancel();
			coverLoadCancel = new CancellationTokenSource();

			if(!song.CheckIsDownloadedAndLoaded()) {
				try {
					XD.FunnyMono(songPreviewPlayer)?.CrossfadeToDefault();
				} catch { }
				coverImage.sprite = await BSSFlowCoordinator.coverLoader.LoadAsync(song.detailsSong, coverLoadCancel.Token);
			} else {
				var h = song.GetCustomLevelIdString();

				songPreviewPlayer = XD.FunnyMono(songPreviewPlayer) ?? Resources.FindObjectsOfTypeAll<SongPreviewPlayer>().FirstOrDefault();

				var preview = beatmapLevelsModel?.GetLevelPreviewForLevelId(h);
				if(preview != null) try {
					levelCollectionViewController?.SongPlayerCrossfadeToLevelAsync(preview);
				} catch { }

				coverImage.sprite = await SongCore.Loader.CustomLevels.Values.First(x => x.levelID == h).GetCoverImageAsync(coverLoadCancel.Token);
			}
			ShowCoverLoader(false);
		}

		internal void SetIsDownloaded(bool isDownloaded, bool downloadable = true) {
			playButton.gameObject.SetActive(isDownloaded);
			playButton.interactable = Manager.goToSongSelect != null;
			downloadButton.gameObject.SetActive(!isDownloaded);

			if(!isDownloaded)
				downloadButton.interactable = downloadable;
		}

		[UIComponent("songDetails")] ModalView songDetails = null;
		[UIComponent("selectedCharacteristics")] TextMeshProUGUI selectedCharacteristics = null;
		[UIComponent("selectedSongKey")] TextMeshProUGUI selectedSongKey = null;
		[UIComponent("selectedSongDescription")] TextMeshProUGUI selectedSongDescription = null;
		[UIComponent("selectedRating")] TextMeshProUGUI selectedRating = null;
		[UIComponent("selectedDownloadCount")] TextMeshProUGUI selectedDownloadCount = null;
		[UIComponent("songDetailsLoading")] ImageView songDetailsLoading = null;

		[UIAction("ShowSongDetauls")]
		void ShowSongDetauls() {
			selectedCharacteristics.text = String.Join(", ", selectedSong.detailsSong.difficulties.GroupBy(x => x.characteristic).Select(x => $"{x.Count()}x {x.Key}"));
			selectedSongKey.text = selectedSong.detailsSong.key;
			selectedDownloadCount.text = selectedSong.detailsSong.downloadCount.ToString("N0");
			selectedRating.text = selectedSong.detailsSong.rating.ToString("0.0%");
			selectedSongDescription.text = "";

			songDetails.Show(true, true);

			songDetailsLoading.gameObject.SetActive(true);

			Task.Run(async () => {
				string desc = "Failed to load description";
				try {
					desc = await SongDownloader.GetSongDescription(selectedSong.detailsSong.key, BSSFlowCoordinator.closeCancelSource.Token);
				} catch { }

				_ = IPA.Utilities.Async.UnityMainThreadTaskScheduler.Factory.StartNew(() => {
					songDetailsLoading.gameObject.SetActive(false);
					// If we dont do that, the description is long and contains unicode the game crashes. Fun.
					selectedSongDescription.text = Regex.Replace(desc, @"\p{Cs}", "?");
				});
			}).ConfigureAwait(false);
		}

		LevelFilteringNavigationController levelFilteringNavigationController = Resources.FindObjectsOfTypeAll<LevelFilteringNavigationController>().FirstOrDefault();
		LevelSearchViewController levelSearchViewController = Resources.FindObjectsOfTypeAll<LevelSearchViewController>().FirstOrDefault();
		LevelCollectionNavigationController levelCollectionNavigationController = Resources.FindObjectsOfTypeAll<LevelCollectionNavigationController>().FirstOrDefault();

		SongSearchSong songToPlayAfterLoading = null;

		internal void PlayQueuedSongToPlay() {
			if(songToPlayAfterLoading == null)
				return;

			PlaySong(songToPlayAfterLoading);
			songToPlayAfterLoading = null;
		}

		[UIAction("Play")] void _Play() => PlaySong();
		internal void PlaySong(SongSearchSong songToPlay = null) {
			if(songToPlay == null)
				songToPlay = selectedSong;

			if(BSSFlowCoordinator.ConfirmCancelOfPending(() => PlaySong(songToPlay)))
				return;

			if(BSSFlowCoordinator.downloadHistoryView.hasUnloadedDownloads) {
				songToPlayAfterLoading = songToPlay;
				SongCore.Loader.Instance.RefreshSongs(false);
				return;
			}

			playButton.interactable = false;

			var level = beatmapLevelsModel?.GetLevelPreviewForLevelId(songToPlay.GetCustomLevelIdString());

			if(level == null)
				return;

			BSSFlowCoordinator.Close(true);

			Manager.goToSongSelect.Invoke();

			if(levelFilteringNavigationController == null)
				return;

			// If this fails for some reason, eh whatever. This is just for preselecting a / the matching diff
			if(songToPlay.diffs.Any(x => x.passesFilter)) try {
					var diffToSelect = songToPlay.GetFirstPassingDifficulty();
					var targetChar = SongCore.Loader.beatmapCharacteristicCollection.GetBeatmapCharacteristicBySerializedName(diffToSelect.detailsDiff.characteristic.ToString().Replace("ThreeSixty", "360").Replace("Ninety", "90"));
					var pData = XD.FunnyMono(BSSFlowCoordinator.playerDataModel)?.playerData;
					if(targetChar != null && pData != null) {
						pData.SetLastSelectedBeatmapCharacteristic(targetChar);
						pData.SetLastSelectedBeatmapDifficulty((BeatmapDifficulty)diffToSelect.detailsDiff.difficulty);
					}
				} catch { }

			// 4 LOC basegame method of selecting a song that works always I LOST
			levelSearchViewController?.ResetCurrentFilterParams();
			levelFilteringNavigationController.UpdateCustomSongs();
			levelFilteringNavigationController.UpdateSecondChildControllerContent(LevelCategory.All);

			levelCollectionNavigationController?.SelectLevel(level);

			ReturnToBSS.returnTobss = PluginConfig.Instance.returnToBssFromSolo;
		}

		[UIAction("Download")]
		void DownloadButton() {
			if(BSSFlowCoordinator.downloadHistoryView.TryAddDownload(selectedSong))
				downloadButton.interactable = false;
		}
	}
}
