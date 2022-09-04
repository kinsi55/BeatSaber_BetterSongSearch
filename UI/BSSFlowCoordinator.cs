using BeatSaberMarkupLanguage;
using BetterSongSearch.Util;
using HMUI;
using SongDetailsCache;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static BetterSongSearch.UI.DownloadHistoryView;

namespace BetterSongSearch.UI {
	class BSSFlowCoordinator : FlowCoordinator {
		internal static FilterView filterView;
		internal static SongListController songListView;
		internal static DownloadHistoryView downloadHistoryView;

		internal static SongAssetAsyncLoader assetLoader = null;
		static internal SongDetails songDetails = null;

		static BSSFlowCoordinator instance = null;

		public static CancellationTokenSource closeCancelSource;

		public static SongSearchSong[] songsList { get; private set; } = null;
		public static SongSearchSong[] filteredSongsListPreallocatedArray { get; private set; } = null;
		public static SongSearchSong[] searchedSongsListPreallocatedArray { get; private set; } = null;

		public static PlayerDataModel playerDataModel = null;
		public static Dictionary<string, Dictionary<string, float>> _songsWithScores = null;
		public static bool songsWithScoresShouldProbablyUpdate = true;

		/*
		 * With the control flow of BSS, this is always accessed off-main-thread when it does happen
		 * to get populated / updated, so we can keep this on whatever thread it happens to get called from
		 */
		public static Dictionary<string, Dictionary<string, float>> songsWithScores {
			get {
				if(_songsWithScores == null || songsWithScoresShouldProbablyUpdate) {
					_songsWithScores ??= new Dictionary<string, Dictionary<string, float>>();
					songsWithScoresShouldProbablyUpdate = false;

					foreach(var x in playerDataModel.playerData.levelsStatsData) {
						if(!x.validScore || x.highScore == 0 || x.levelID.Length < 13 + 40 || !x.levelID.StartsWith("custom_level_", StringComparison.Ordinal))
							continue;

						var sh = x.levelID.Substring(13, 40);

						SongDetailsCache.Structs.Song song;
						// local score level id's can be scuffed if you pass custom levels w/ no songcore installed
						try {
							if(!songDetails.songs.FindByHash(sh, out song))
								continue;
						} catch { continue; }

						if(!song.GetDifficulty(out var diff, (SongDetailsCache.Structs.MapDifficulty)x.difficulty))
							continue;

						if(!_songsWithScores.TryGetValue(sh, out var h))
							_songsWithScores.Add(sh, h = new Dictionary<string, float>());

						/*
						 * Calculating the maxscore for a map is now a bajillion times more complex than it was before
						 * bye bye sort by worst score
						 */
						//var maxScore = ScoreModel.MaxRawScoreForNumberOfNotes((int)diff.notes);
						//h[$"{x.beatmapCharacteristic.serializedName}_{x.difficulty}"] = (x.highScore * 100f) / maxScore;
						h[$"{x.beatmapCharacteristic.serializedName}_{x.difficulty}"] = 0;
					}
				}

				return _songsWithScores;
			}
		}

		protected async override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling) {
			instance = this;

			closeCancelSource = new CancellationTokenSource();

			assetLoader ??= new SongAssetAsyncLoader();

			playerDataModel ??= XD.FunnyMono(playerDataModel) ?? UnityEngine.Object.FindObjectOfType<PlayerDataModel>();

			static async Task DataUpdated() {
				_ = IPA.Utilities.Async.UnityMainThreadTaskScheduler.Factory.StartNew(() => {
					filterView.datasetInfoLabel?.SetText($"{songDetails.songs.Length} songs in dataset | Newest: {songDetails.songs.Last().uploadTime.ToLocalTime():d\\. MMM yy - HH:mm}");
				});

				await Task.Run(() => {
					songsList = new SongSearchSong[songDetails.songs.Length];
					filteredSongsListPreallocatedArray = new SongSearchSong[songsList.Length];
					searchedSongsListPreallocatedArray = new SongSearchSong[songsList.Length];

					for(var i = 0; i < songsList.Length; i++)
						songsList[i] = new SongSearchSong(songDetails.songs[i]);
				});

				FilterSongs();
			};

			if(firstActivation) {
				SetTitle("Better Song Search");

				showBackButton = true;

				filterView = BeatSaberUI.CreateViewController<FilterView>();
				songListView = BeatSaberUI.CreateViewController<SongListController>();
				downloadHistoryView = BeatSaberUI.CreateViewController<DownloadHistoryView>();

				ProvideInitialViewControllers(songListView, filterView, downloadHistoryView);

				SongCore.Loader.SongsLoadedEvent += SongcoreSongsLoaded;
				SongDetailsContainer.dataAvailableOrUpdated += () => _ = DataUpdated();

				BeatSaverRegionManager.RegionLookup();
			}
			// Re-Init every time incase its time to download a new database
			songDetails = await SongDetails.Init(1);

			await DataUpdated();

			if(!firstActivation)
				downloadHistoryView.RefreshTable();
		}

		void SongcoreSongsLoaded(object a, object b) {
			foreach(var x in downloadHistoryView.downloadList)
				if(x.status == DownloadHistoryEntry.DownloadStatus.Downloaded)
					x.status = DownloadHistoryEntry.DownloadStatus.Loaded;

			downloadHistoryView.RefreshTable(false);

			songListView.selectedSongView.PlayQueuedSongToPlay();
		}

		static internal int lastVisibleTableRowIdx { get; private set; } = 0;

		static Action cancelConfirmCallback = null;
		public static bool ConfirmCancelOfPending(Action confirmCallback) {
			if(downloadHistoryView.downloadList.Any(x => x.isDownloading || x.isQueued)) {
				cancelConfirmCallback = confirmCallback;
				songListView.ShowCloseConfirmation();

				return true;
			}
			return false;
		}

		public static void ConfirmCancelCallback(bool doCancel = true) {
			if(doCancel) {
				foreach(var x in downloadHistoryView.downloadList) {
					if(!x.isDownloading && !x.isQueued)
						continue;

					x.retries = 69;
					x.status = DownloadHistoryEntry.DownloadStatus.Failed;
				}
				closeCancelSource?.Cancel();

				cancelConfirmCallback?.Invoke();
			}

			cancelConfirmCallback = null;
		}

		/// <summary>
		/// Cloases the BetterSongSearch Flow
		/// </summary>
		/// <param name="immediately">True = Close immediately without transition</param>
		/// <param name="downloadAbortConfim">True = Confirm closing if there is pending downloads</param>
		public static void Close(bool immediately = false, bool downloadAbortConfim = true) {
			if(downloadAbortConfim && ConfirmCancelOfPending(() => Close(immediately, false)))
				return;

			cancelConfirmCallback = null;
			SelectedSongView.songAssetLoadCanceller?.Cancel();
			try {
				XD.FunnyMono(SelectedSongView.songPreviewPlayer)?.CrossfadeToDefault();
			} catch { }

			foreach(var x in songListView.GetComponentsInChildren<ModalView>()) {
				x.Hide(false);
				//x.gameObject.SetActive(false);
			}

			if(downloadHistoryView.hasUnloadedDownloads)
				SongCore.Loader.Instance.RefreshSongs(false);

			if(instance != null) Manager._parentFlow.DismissFlowCoordinator(instance, () => {
				lastVisibleTableRowIdx = songListView.songList.GetVisibleCellsIdRange().Item1;
				songsList = null;
				filteredSongsListPreallocatedArray = null;
				searchedSongsListPreallocatedArray = null;
				SongListController.filteredSongsList = null;
				SongListController.searchedSongsList = null;
				WeightedSongSearch.cachedSearchableStrings = null;
				songsWithScoresShouldProbablyUpdate = true;
				songListView.songList.ReloadData();

				assetLoader?.Dispose();
				assetLoader = null;

				instance = null;
			}, ViewController.AnimationDirection.Horizontal, immediately);
		}

		protected override void BackButtonWasPressed(ViewController topViewController) => Close();

		public static async void FilterSongs() {
			if(songDetails == null)
				return;

			await Task.Run(() => {
				var sc = 0;

				// Loop through our (custom) songdetails array
				for(var i = 0; i < songsList.Length; i++) {
					/*
					 * Since our custom array is recreated whenever songDetails updates we can
					 * get the song directly by ref from songdetails as the index matches
					 */
					ref var val = ref songDetails.songs[i];

					// Check if the song itself passes the filter
					if(!filterView.SongCheck(in val) || !filterView.SearchSongCheck(songsList[i]))
						continue;

					var hasAnyValid = false;

					/*
					 * loop all diffs of this song to see if any diff matches our filter.
					 * for those diffs that we checked we pre-set passesFilter so that it
					 * doesnt need to get (re)checked later whenever the diffs array is accessed
					 */
					var theThing = songsList[i];

					for(var iDiff = 0; iDiff < val.diffCount; iDiff++) {
						var theDiff = theThing.diffs[iDiff];

						theDiff._passesFilter = null;
						if(!hasAnyValid)
							hasAnyValid = theDiff.passesFilter;
					}

					if(!hasAnyValid)
						continue;

					filteredSongsListPreallocatedArray[sc++] = theThing;
				}

				SongListController.filteredSongsList = new ArraySegment<SongSearchSong>(filteredSongsListPreallocatedArray, 0, sc);
			});

			songListView.UpdateSearchedSongsList();
		}
	}
}
