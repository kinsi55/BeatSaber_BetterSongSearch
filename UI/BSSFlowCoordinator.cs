using BeatSaberMarkupLanguage;
using BetterSongSearch.Util;
using HMUI;
using SongDetailsCache;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace BetterSongSearch.UI {
	class BSSFlowCoordinator : FlowCoordinator {
		internal static FilterView filterView;
		internal static SongListController songListView;
		internal static DownloadHistoryView downloadHistoryView;

		internal static CoverImageAsyncLoader coverLoader = null;
		static internal SongDetails songDetails = null;

		static BSSFlowCoordinator instance = null;

		public static CancellationTokenSource closeCancelSource;

		public static SongSearchSong[] songsList { get; private set; } = null;

		protected async override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling) {
			instance = this;

			closeCancelSource = new CancellationTokenSource();

			coverLoader ??= new CoverImageAsyncLoader();

			void DataUpdated() {
				songsList = new SongSearchSong[songDetails.songs.Length];

				for(var i = 0; i < songsList.Length; i++)
					songsList[i] = new SongSearchSong(songDetails.songs[i]);

				FilterSongs();

				IPA.Utilities.Async.UnityMainThreadTaskScheduler.Factory.StartNew(() => {
					filterView.datasetInfoLabel?.SetText($"{songDetails.songs.Length} songs in dataset | Newest: {songDetails.songs.Last().uploadTime:d\\. MMM yy - HH:mm}");
				});
			};

			if(firstActivation) {
				SetTitle("Better Song Search");

				filterView = BeatSaberUI.CreateViewController<FilterView>();
				songListView = BeatSaberUI.CreateViewController<SongListController>();
				downloadHistoryView = BeatSaberUI.CreateViewController<DownloadHistoryView>();

				ProvideInitialViewControllers(songListView, filterView, downloadHistoryView);

				SongCore.Loader.SongsLoadedEvent += SongcoreSongsLoaded;
				SongDetailsContainer.dataAvailableOrUpdated += DataUpdated;

				showBackButton = true;

				songDetails ??= await SongDetails.Init();

				DataUpdated();
			} else {
				DataUpdated();

				downloadHistoryView.RefreshTable();
			}
		}

		void SongcoreSongsLoaded(object a, object b) {
			foreach(var x in downloadHistoryView.downloadList)
				if(x.status == DownloadHistoryView.Entry.DownloadStatus.Downloaded)
					x.status = DownloadHistoryView.Entry.DownloadStatus.Loaded;

			downloadHistoryView.RefreshTable(false);

			songListView.selectedSongView.PlayQueuedSongToPlay();
		}

		/// <summary>
		/// Cloases the BetterSongSearch Flow
		/// </summary>
		/// <param name="immediately">True = Close immediately without transition</param>
		/// <param name="downloadAbortConfim">True = Confirm closing if there is pending downloads</param>
		public static void Close(bool immediately = false, bool downloadAbortConfim = true) {
			if(downloadAbortConfim && downloadHistoryView.downloadList.Any(x => x.status == DownloadHistoryView.Entry.DownloadStatus.Downloading)) {
				songListView.ShowCloseConfirmation();

				return;
			}

			closeCancelSource?.Cancel();

			SelectedSongView.coverLoadCancel?.Cancel();
			XD.FunnyMono(SelectedSongView.songPreviewPlayer)?.CrossfadeToDefault();

			foreach(var x in filterView.GetComponentsInChildren<ModalView>())
				x.enabled = false;

			foreach(var x in songListView.GetComponentsInChildren<ModalView>())
				x.enabled = false;

			if(downloadHistoryView.hasUnloadedDownloads)
				SongCore.Loader.Instance.RefreshSongs();

			if(instance != null) Manager._parentFlow.DismissFlowCoordinator(instance, () => {
				songsList = null;
				SongListController.filteredSongsList = null;
				SongListController.searchedSongsList = null;

				coverLoader?.Dispose();
				coverLoader = null;

				instance = null;

				GC.Collect();
			}, ViewController.AnimationDirection.Horizontal, immediately);
		}

		protected override void BackButtonWasPressed(ViewController topViewController) => Close();

		public static async void FilterSongs() {
			if(songDetails == null)
				return;

			await Task.Run(() => {
				var nl = new List<SongSearchSong>();
				SongListController.filteredSongsList = nl;

				// Loop through our (custom) songdetails array
				for(var i = 0; i < songsList.Length; i++) {
					/*
					 * Since our custom array is recreated whenever songDetails updates we can
					 * get the song directly by ref from songdetails as the index matches
					 */
					ref var val = ref songDetails.songs[i];

					// Check if the song itself passes the filter
					if(!filterView.SongCheck(in val))
						continue;

					bool hasAnyValid = false;

					/*
					 * loop all diffs of this song to see if any diff matches our filter.
					 * for those diffs that we checked we pre-set passesFilter so that it
					 * doesnt need to get (re)checked later whenever the diffs array is accessed
					 */
					ref var theThing = ref songsList[i];

					for(var iDiff = 0; iDiff < val.diffCount; iDiff++) {
						ref var theDiff = ref theThing.diffs[iDiff];

						if(!hasAnyValid) {
							var doesPass = filterView.DifficultyCheck(in theDiff.detailsDiff);
							theDiff._passesFilter = hasAnyValid = doesPass;
						} else {
							// Defer checking further diffs until later (See passesFilter getter)
							theDiff._passesFilter = null;
						}
					}

					if(!hasAnyValid)
						continue;

					songsList[i]._sortedDiffsCache = null;

					nl.Add(songsList[i]);
				}
			});

			songListView.UpdateSearchedSongsList();
		}
	}
}
