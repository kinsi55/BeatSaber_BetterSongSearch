using BeatSaberMarkupLanguage;
using HMUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SongDetailsCache;
using SongDetailsCache.Structs;
using System.Diagnostics;
using BetterSongSearch.Util;
using UnityEngine;
using IPA.Utilities;
using System.Threading;

namespace BetterSongSearch.UI {
	class UIMainFlowCoordinator : FlowCoordinator {
		internal static FilterView filterView;
		internal static SongListController songListView;
		internal static DownloadHistoryView downloadHistoryView;

		internal static CoverImageAsyncLoader coverLoader = null;
		static internal SongDetails songDetails = null;

		static UIMainFlowCoordinator instance = null;

		public static CancellationTokenSource closeCancelSource;

		protected async override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling) {
			instance = this;

			closeCancelSource = new CancellationTokenSource();

			coverLoader ??= new CoverImageAsyncLoader();

			void dataUpdated() { 
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
				SongDetailsContainer.dataAvailableOrUpdated += dataUpdated;

				showBackButton = true;

				songDetails ??= await SongDetails.Init();

				dataUpdated();
			} else {
				FilterSongs();

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
			SelectedSongView.songPreviewPlayer?.CrossfadeToDefault();

			foreach(var x in filterView.GetComponentsInChildren<ModalView>())
				x.enabled = false;

			foreach(var x in songListView.GetComponentsInChildren<ModalView>())
				x.enabled = false;

			if(downloadHistoryView.hasUnloadedDownloads)
				SongCore.Loader.Instance.RefreshSongs();

			if(instance != null) Manager._parentFlow.DismissFlowCoordinator(instance, () => {
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
				// Debating if its worth to pre-create an array of SongSearchSong's and filtering those, probably not.
				SongListController.filteredSongsList =
					songDetails.FindSongs((in SongDifficulty diff) =>
						filterView.DifficultyCheck(in diff) &&
						filterView.SongCheck(in diff.song)
					)
					.Select(x => new SongSearchSong(in x));
			});

			songListView.UpdateSearchedSongsList();
		}
	}
}
