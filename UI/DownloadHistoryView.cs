using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components;
using BeatSaberMarkupLanguage.ViewControllers;
using BetterSongSearch.Util;
using HMUI;
using IPA.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BetterSongSearch.UI {
	[HotReload(RelativePathToLayout = @"Views\DownloadHistory.bsml")]
	[ViewDefinition("BetterSongSearch.UI.Views.DownloadHistory.bsml")]
	class DownloadHistoryView : BSMLAutomaticViewController {
		[UIComponent("scrollBarContainer")] private VerticalLayoutGroup _scrollBarContainer = null;
		[UIComponent("downloadList")] CustomCellListTableData downloadHistoryData = null;
		TableView downloadHistoryTable => downloadHistoryData?.tableView;
		public readonly List<DownloadHistoryEntry> downloadList = new List<DownloadHistoryEntry>();

		public bool hasUnloadedDownloads => downloadList.Any(x => x.status == DownloadHistoryEntry.DownloadStatus.Downloaded);

		const int RETRY_COUNT = 3;
		const int MAX_PARALLEL_DOWNLOADS = 2;

		public bool TryAddDownload(SongSearchSong song, bool isBatch = false) {
			var existingDLHistoryEntry = downloadList.FirstOrDefault(x => x.key == song.detailsSong.key);

			existingDLHistoryEntry?.ResetIfFailed();

			if(!song.CheckIsDownloadable())
				return false;

			if(existingDLHistoryEntry == null) {
				downloadList.Insert(0, new DownloadHistoryEntry(song));
			} else {
				existingDLHistoryEntry.status = DownloadHistoryEntry.DownloadStatus.Queued;
			}

			ProcessDownloads(!isBatch);

			return true;
		}

		void SelectSong(TableView _, DownloadHistoryEntry row) {
			if(BSSFlowCoordinator.songDetails.songs.FindByMapId(row.key, out var song))
				BSSFlowCoordinator.songListView.selectedSongView.SetSelectedSong(BSSFlowCoordinator.songsList[song.index]);
		}

		public async void ProcessDownloads(bool forceTableReload = false) {
			if(!gameObject.activeInHierarchy)
				return;

			if(downloadList.Count(x => x.IsInAnyOfStates(DownloadHistoryEntry.DownloadStatus.Preparing | DownloadHistoryEntry.DownloadStatus.Downloading)) >= MAX_PARALLEL_DOWNLOADS) {
				if(forceTableReload)
					RefreshTable(true);
				return;
			}

			var firstEntry = downloadList
				.Where(x => x.retries < RETRY_COUNT && x.IsInAnyOfStates(DownloadHistoryEntry.DownloadStatus.Failed | DownloadHistoryEntry.DownloadStatus.Queued))
				.OrderBy(x => x.orderValue)
				.FirstOrDefault();

			if(firstEntry == null) {
				RefreshTable(forceTableReload);
				return;
			}

			if(firstEntry.status == DownloadHistoryEntry.DownloadStatus.Failed)
				firstEntry.retries++;

			firstEntry.downloadProgress = 0f;
			firstEntry.status = DownloadHistoryEntry.DownloadStatus.Preparing;

			RefreshTable(true);

			await Task.Run(async () => {
				try {
					var updateRateLimiter = new Stopwatch();
					updateRateLimiter.Start();

					await SongDownloader.BeatmapDownload(firstEntry, BSSFlowCoordinator.closeCancelSource.Token, (float progress) => {
						firstEntry.statusDetails = string.Format("({0:0%}{1})", progress, firstEntry.retries == 0 ? "" : $", retry #{firstEntry.retries} / ");
						firstEntry.downloadProgress = progress;

						if(updateRateLimiter.ElapsedMilliseconds < 0.05)
							return;
						updateRateLimiter.Restart();

						IPA.Utilities.Async.UnityMainThreadTaskScheduler.Factory.StartNew(firstEntry.UpdateProgress);
					});

					firstEntry.status = DownloadHistoryEntry.DownloadStatus.Downloaded;
					firstEntry.statusDetails = "";
				} catch(Exception ex) {
#if DEBUG
					if(!(ex is TaskCanceledException)) {
						Plugin.Log.Warn("Download failed:");
						Plugin.Log.Warn(ex);
					}
#endif

					firstEntry.status = DownloadHistoryEntry.DownloadStatus.Failed;
					firstEntry.statusDetails = $"{(firstEntry.retries < 3 ? "(Will retry)" : "")}: More details in log, {ex.GetType().Name}";
				}
			});

			if(firstEntry.status == DownloadHistoryEntry.DownloadStatus.Downloaded) {
				BSSFlowCoordinator.songListView.songList.RefreshCells(false, true);

				// NESTING HELLLL
				var selectedSongView = BSSFlowCoordinator.songListView.selectedSongView;
				if(selectedSongView.selectedSong.detailsSong.key == firstEntry.key)
					selectedSongView.SetIsDownloaded(true);
			}

			ProcessDownloads(true);
		}

		RatelimitCoroutine limitedFullTableReload;

		void Awake() {
			limitedFullTableReload = new RatelimitCoroutine(() => {
				BSMLStuff.UnleakTable(downloadHistoryTable.gameObject);

				downloadHistoryData.data = downloadList.OrderBy(x => x.orderValue).ToList<object>();

				downloadHistoryTable.ReloadData();
				downloadHistoryTable.ScrollToCellWithIdx(0, TableView.ScrollPositionType.Beginning, false);
			}, 0.2f);
		}

		public void RefreshTable(bool fullReload = true) {
			if(fullReload) {
				StartCoroutine(limitedFullTableReload.Call());
			} else {
				downloadHistoryTable.RefreshCells(false, true);
			}
		}


		[UIAction("#post-parse")]
		void Parsed() {
			ReflectionUtil.SetField(downloadHistoryTable, "_canSelectSelectedCell", true);

			BSMLStuff.GetScrollbarForTable(downloadHistoryData.gameObject, _scrollBarContainer.transform);
		}

		public class DownloadHistoryEntry {
			[Flags]
			public enum DownloadStatus : byte {
				Downloading = 1,
				Preparing = 2,
				Extracting = 4,
				Queued = 8,
				Failed = 16,
				Downloaded = 32,
				Loaded = 64
			}

			public DownloadStatus status = DownloadStatus.Queued;
			public string statusMessage => $"{status} {statusDetails}";
			public string statusDetails = "";
			public float downloadProgress = 1f;

			public int retries = 0;

			public readonly string songName;
			public readonly string levelAuthorName;
			public readonly string key;
			public readonly string hash;

			public int orderValue => ((int)status * 100) + retries;

			public bool IsInAnyOfStates(DownloadStatus states) {
				return (status & states) != 0;
			}

			public void ResetIfFailed() {
				if(status != DownloadStatus.Failed || retries != RETRY_COUNT)
					return;

				status = DownloadStatus.Queued;
				retries = 0;
			}

			public DownloadHistoryEntry(SongSearchSong song) {
				songName = song.detailsSong.songName;
				levelAuthorName = song.detailsSong.levelAuthorName;
				key = song.detailsSong.key.ToLower();
				hash = song.detailsSong.hash.ToLower();
			}

			[UIComponent("statusLabel")] TextMeshProUGUI statusLabel = null;
			[UIComponent("bgContainer")] ImageView bg = null;
			[UIComponent("bgProgress")] ImageView bgProgress = null;
			RectTransform bgProgressRect = null;
			[UIAction("refresh-visuals")]
			public void Refresh(bool selected, bool highlighted) {
				bg.color = new Color(0, 0, 0, highlighted ? 0.8f : 0.45f);

				RefreshBar();
			}

			[UIAction("#post-parse")]
			void RefreshBar() {
				var clr = status == DownloadStatus.Failed ? Color.red : status != DownloadStatus.Queued ? Color.green : Color.gray;

				clr.a = 0.5f + (downloadProgress * 0.4f);
				bgProgress.color = clr;

				if((bgProgressRect = bgProgress.gameObject.GetComponent<RectTransform>()) == null)
					return;

				bgProgressRect.anchorMax = new Vector2(downloadProgress, 1);
				bgProgressRect.ForceUpdateRectTransforms();
			}

			public void UpdateProgress() {
				statusLabel.text = statusMessage;

				RefreshBar();
			}
		}
	}
}
