using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components;
using BeatSaberMarkupLanguage.ViewControllers;
using BetterSongSearch.Util;
using HMUI;
using IPA.Utilities;
using System;
using System.Collections.Generic;
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
		public readonly List<Entry> downloadList = new List<Entry>();

		public bool hasUnloadedDownloads => downloadList.Any(x => x.status == Entry.DownloadStatus.Downloaded);

		const int RETRY_COUNT = 3;

		public bool TryAddDownload(SongSearchSong song) {
			var existingDLHistoryEntry = downloadList.FirstOrDefault(x => x.key == song.detailsSong.key);

			existingDLHistoryEntry?.ResetIfFailed();

			if(!song.CheckIsDownloadable())
				return false;

			if(existingDLHistoryEntry == null) {
				downloadList.Insert(0, new Entry(song));
			} else {
				existingDLHistoryEntry.status = Entry.DownloadStatus.Queued;
			}

			RefreshTable(true);

			ProcessDownloads();

			return true;
		}

		void SelectSong(TableView _, Entry row) {
			if(BSSFlowCoordinator.songDetails.songs.FindByMapId(row.key, out var song))
				BSSFlowCoordinator.songListView.selectedSongView.SetSelectedSong(BSSFlowCoordinator.songsList[song.index]);
		}

		public async void ProcessDownloads() {
			if(!gameObject.activeInHierarchy)
				return;

			downloadListEntries = downloadList.OrderByDescending(x => x.status).Cast<object>().ToList();

			if(downloadList.Any(x => x.status == Entry.DownloadStatus.Downloading || x.status == Entry.DownloadStatus.Extracting))
				return;

			var firstEntry = downloadList
				.Where(x => x.retries < RETRY_COUNT)
				.OrderBy(x => x.retries)
				.FirstOrDefault(x => x.status != Entry.DownloadStatus.Downloaded && x.status != Entry.DownloadStatus.Loaded);

			if(firstEntry == null) {
				RefreshTable(false);
				return;
			}

			if(firstEntry.status == Entry.DownloadStatus.Failed)
				firstEntry.retries++;

			firstEntry.downloadProgress = 0f;
			firstEntry.status = Entry.DownloadStatus.Preparing;

			RefreshTable(true);

			await Task.Run(async () => {
				try {
					await SongDownloader.BeatmapDownload(firstEntry, BSSFlowCoordinator.closeCancelSource.Token, (float progress) => {
						firstEntry.statusDetails = string.Format("({0:0%}{1})", progress, firstEntry.retries == 0 ? "" : $", retry #{firstEntry.retries}");
						firstEntry.downloadProgress = progress;

						IPA.Utilities.Async.UnityMainThreadTaskScheduler.Factory.StartNew(firstEntry.UpdateProgress);
					});

					firstEntry.status = Entry.DownloadStatus.Downloaded;
					firstEntry.statusDetails = "";
				} catch(Exception ex) {
#if DEBUG
					Plugin.Log.Critical(ex);
#endif

					firstEntry.status = Entry.DownloadStatus.Failed;
					firstEntry.statusDetails = $"{(firstEntry.retries < 3 ? "(Will retry)" : "")}: {ex.Message}";
				}
			});

			if(firstEntry.status == Entry.DownloadStatus.Downloaded) {
				BSSFlowCoordinator.songListView.RefreshTable();

				// NESTING HELLLL
				var selectedSongView = BSSFlowCoordinator.songListView.selectedSongView;
				if(selectedSongView.selectedSong.detailsSong.key == firstEntry.key)
					selectedSongView.SetIsDownloaded(true);
			}

			ProcessDownloads();
		}

		public void RefreshTable(bool fullReload = true) {
			BSMLStuff.UnleakTable(downloadHistoryTable.gameObject);

			downloadHistoryData.data = downloadListEntries = downloadList.OrderBy(x => (int)x.status).Cast<object>().ToList();

			if(fullReload) {
				downloadHistoryTable.ReloadData();
				downloadHistoryTable.ScrollToCellWithIdx(0, TableView.ScrollPositionType.Beginning, false);
			} else {
				downloadHistoryTable.RefreshCells(false, true);
			}
		}


		[UIAction("#post-parse")]
		void Parsed() {
			ReflectionUtil.SetField(downloadHistoryTable, "_canSelectSelectedCell", true);

			BSMLStuff.GetScrollbarForTable(downloadHistoryData.gameObject, _scrollBarContainer.transform);
		}

		public class Entry {
			public enum DownloadStatus {
				Queued,
				Preparing,
				Downloading,
				Extracting,
				Downloaded,
				Loaded,
				Failed
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

			public void ResetIfFailed() {
				if(status != DownloadStatus.Failed || retries != RETRY_COUNT)
					return;

				status = DownloadStatus.Queued;
				retries = 0;
			}

			public Entry(SongSearchSong song) {
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
