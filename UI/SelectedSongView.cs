using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Parser;
using BetterSongSearch.HarmonyPatches;
using BetterSongSearch.Util;
using HarmonyLib;
using HMUI;
using System.Linq;
using System.Reflection;
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
		[UIComponent("coverImage")] internal readonly Image coverImage = null;
		[UIComponent("selectedSongAuthor")] TextMeshProUGUI selectedSongAuthor = null;
		[UIComponent("selectedSongName")] TextMeshProUGUI selectedSongName = null;
		[UIComponent("selectedSongDiffInfo")] TextMeshProUGUI selectedSongDiffInfo = null;

		[UIComponent("downloadButton")] NoTransitionsButton downloadButton = null;
		[UIComponent("playButton")] NoTransitionsButton playButton = null;

		[UIComponent("detailActions")] Transform detailActionsHideUntilSongsAvailable = null;

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

		void OnEnable() {
			if(playButton != null)
				playButton.interactable = true;

			if(selectedSong != null)
				SetIsDownloaded(selectedSong.CheckIsDownloaded(), selectedSong.CheckIsDownloadable());
		}

		static internal SongPreviewPlayer songPreviewPlayer { get; private set; } = null;
		static BeatmapLevelsModel _beatmapLevelsModel = null;
		static BeatmapLevelsModel beatmapLevelsModel => XD.FunnyMono(_beatmapLevelsModel) ?? (_beatmapLevelsModel = Resources.FindObjectsOfTypeAll<BeatmapLevelsModel>().FirstOrDefault(x => x.customLevelPackCollection != null));

		static LevelCollectionViewController _levelCollectionViewController;
		static LevelCollectionViewController levelCollectionViewController => XD.FunnyMono(_levelCollectionViewController) ?? (_levelCollectionViewController = Resources.FindObjectsOfTypeAll<LevelCollectionViewController>().FirstOrDefault());


		static internal CancellationTokenSource songAssetLoadCanceller { get; private set; } = null;
		internal async void SetSelectedSong(SongSearchSong song, bool selectInTableIfPossible = false) {
			if(song == null || songToPlayAfterLoading != null)
				return;

			var prevKey = selectedSong?.detailsSong.mapId;

			selectedSong = song;

			//TODO: Mabye disable download button when song is already queued
			SetIsDownloaded(song.CheckIsDownloaded(), song.CheckIsDownloadable());

			selectedSongAuthor.text = song.detailsSong.songAuthorName;
			selectedSongName.text = song.detailsSong.songName;

			detailActionsHideUntilSongsAvailable.gameObject.SetActive(true);

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

			songAssetLoadCanceller?.Cancel();
			songAssetLoadCanceller = new CancellationTokenSource();

			songPreviewPlayer = XD.FunnyMono(songPreviewPlayer) ?? Resources.FindObjectsOfTypeAll<SongPreviewPlayer>().FirstOrDefault();

			if(!song.CheckIsDownloadedAndLoaded()) {
				try {
					XD.FunnyMono(songPreviewPlayer)?.CrossfadeToDefault();

					await Task.WhenAll(new[] {
						BSSFlowCoordinator.assetLoader.LoadCoverAsync(song.detailsSong, songAssetLoadCanceller.Token).ContinueWith(
							x => { coverImage.sprite = x.Result; },
							TaskContinuationOptions.OnlyOnRanToCompletion
						),

						!PluginConfig.Instance.loadSongPreviews ? Task.FromResult(1) : BSSFlowCoordinator.assetLoader.LoadPreviewAsync(song.detailsSong, songAssetLoadCanceller.Token).ContinueWith(
							x => { if(x.Result != null && songAssetLoadCanceller?.IsCancellationRequested == false) XD.FunnyMono(songPreviewPlayer)?.CrossfadeTo(x.Result, -5f, 0, x.Result.length, null); },
							TaskContinuationOptions.OnlyOnRanToCompletion
						)
					});
				} catch { }
			} else {
				var h = song.GetCustomLevelIdString();

				var preview = beatmapLevelsModel?.GetLevelPreviewForLevelId(h);
				try {
					if(preview != null)
						levelCollectionViewController?.SongPlayerCrossfadeToLevelAsync(preview);
					coverImage.sprite = await SongCore.Loader.CustomLevels.Values.FirstOrDefault(x => x.levelID == h)?.GetCoverImageAsync(songAssetLoadCanceller.Token);
				} catch { }
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

		BSMLParserParams detailsParams = null;
		[UIAction("ShowSongDetails")]
		void ShowSongDetails() {
			BSMLStuff.InitSplitView(ref detailsParams, gameObject, SplitViews.UploadDetails.instance).EmitEvent("ShowModal");

			SplitViews.UploadDetails.instance.Populate(selectedSong);
		}

		void FilterByUploader() {
			FilterView.currentFilter.uploadersString = selectedSong.detailsSong.uploaderName.ToLowerInvariant();
			FilterView.currentFilter.NotifyPropertyChanged("uploadersString");
			BSSFlowCoordinator.FilterSongs();
		}

		SongSearchSong songToPlayAfterLoading = null;

		internal void PlayQueuedSongToPlay() {
			if(songToPlayAfterLoading == null)
				return;

			PlaySong(songToPlayAfterLoading);
			songToPlayAfterLoading = null;
		}

		readonly SoloFreePlayFlowCoordinator soloFreePlayFlowCoordinator = FindObjectOfType<SoloFreePlayFlowCoordinator>();
		readonly MultiplayerLevelSelectionFlowCoordinator multiplayerLevelSelectionFlowCoordinator = FindObjectOfType<MultiplayerLevelSelectionFlowCoordinator>();
		static readonly ConstructorInfo LevelSelectionFlowCoordinator_State = AccessTools.FirstConstructor(typeof(LevelSelectionFlowCoordinator.State), x => x.GetParameters().Length == 4);

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
			ReturnToBSS.returnTobss = PluginConfig.Instance.returnToBssFromSolo;

			// If this fails for some reason, eh whatever. This is just for preselecting a / the matching diff
			if(BSSFlowCoordinator.playerDataModel != null && songToPlay.GetFirstPassingDifficulty() != null) {
				try {
					var ddiff = songToPlay.GetFirstPassingDifficulty().detailsDiff;

					var targetChar = SongCore.Loader.beatmapCharacteristicCollection.GetBeatmapCharacteristicBySerializedName(ddiff.characteristic.ToString().Replace("ThreeSixty", "360").Replace("Ninety", "90"));

					var pData = BSSFlowCoordinator.playerDataModel.playerData;

					if(targetChar != null) {
						pData.SetLastSelectedBeatmapCharacteristic(targetChar);
						pData.SetLastSelectedBeatmapDifficulty((BeatmapDifficulty)ddiff.difficulty);
					}
				} catch { }
			}

			var x = (LevelSelectionFlowCoordinator.State)LevelSelectionFlowCoordinator_State.Invoke(new object[] {
				LevelCategory.All,
				SongCore.Loader.CustomLevelsPack,
				level,
				null
			});

			multiplayerLevelSelectionFlowCoordinator.Setup(x);
			soloFreePlayFlowCoordinator.Setup(x);

			Manager.goToSongSelect.Invoke();
		}

		[UIAction("Download")]
		void DownloadButton() {
			if(BSSFlowCoordinator.downloadHistoryView.TryAddDownload(selectedSong))
				downloadButton.interactable = false;
		}
	}
}
