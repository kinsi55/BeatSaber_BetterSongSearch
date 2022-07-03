using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Unity.Collections;
using UnityEngine.Networking;
using static BetterSongSearch.UI.DownloadHistoryView;

namespace BetterSongSearch.Util {
	static class SongDownloader {
		public static async Task BeatmapDownload(DownloadHistoryEntry entry, CancellationToken token, Action<float> progressCb) {
			var folderName = $"{entry.key} ({entry.songName} - {entry.levelAuthorName})";

			var baseUrl = PluginConfig.Instance.downloadUrlOverride;

			if(baseUrl.Length == 0)
				baseUrl = entry.retries == 0 ? BeatSaverRegionManager.mapDownloadUrl : BeatSaverRegionManager.mapDownloadUrlFallback;

			var dl = await UnityWebrequestWrapper.DownloadBytes($"{baseUrl}/{entry.hash}.zip".ToLowerInvariant(), token, (p) => {
				entry.status = DownloadHistoryEntry.DownloadStatus.Downloading;
				progressCb(p);
			});

			var t = new CancellationTokenSource();
			token.Register(t.Cancel);

			using(var s = new MemoryStream(dl)) {
				entry.status = DownloadHistoryEntry.DownloadStatus.Extracting;
				progressCb(0);

				// Not async'ing this as BeatmapDownload() is supposed to be called in a task
				ExtractZip(s, folderName, t.Token, progressCb);
			}
		}

		static unsafe void ExtractZip(Stream zipStream, string basePath, CancellationToken token, Action<float> progressCb, bool overwrite = false) {
			basePath = string.Concat(basePath.Split(Path.GetInvalidFileNameChars())).Trim();

			int steps;
			var progress = 0;
			var files = new Dictionary<string, (IntPtr ptr, UnmanagedMemoryStream stream)>();

			try {
				var longestFileNameLength = 0;

				// Unzip everything to memory first so we dont end up writing half a song incase something breaks
				using(var archive = new ZipArchive(zipStream, ZipArchiveMode.Read)) {
					steps = archive.Entries.Count() * 2;

					foreach(var entry in archive.Entries) {
						var len = (int)entry.Length;

						// If a file, supposedly, is bigger than that we can assume its malicious
						if(len > 200_000_000)
							throw new InvalidDataException();

						// Dont extract directories / sub-files
						if(entry.FullName.IndexOf("/", StringComparison.Ordinal) == -1) {
							using(var str = entry.Open()) {
								var file = Marshal.AllocHGlobal(len);
								var x = new UnmanagedMemoryStream((byte*)file, len, len, FileAccess.ReadWrite);

								str.CopyTo(x);

								x.Position = 0;

								files.Add(entry.Name, (file, x));

								if(entry.Name.Length > longestFileNameLength)
									longestFileNameLength = entry.Name.Length;
							}
						} else {
							// As this wont extract anthing further down we need to increase the process for it in advance
							progress++;
						}

						progressCb((float)++progress / steps);
					}
				}

				// Failsafe so we dont break songcore. Info.dat, a diff and the song itself - not sure if the cover is needed
				if(files.Count < 3 || !files.Keys.Any(x => x.Equals("info.dat", StringComparison.OrdinalIgnoreCase)))
					throw new InvalidDataException();

				token.ThrowIfCancellationRequested();

				var path = Path.Combine(Directory.GetCurrentDirectory(), CustomLevelPathHelper.customLevelsDirectoryPath, basePath);

				if(path.Length > 253 - longestFileNameLength)
					path = $"{path.Substring(0, 253 - longestFileNameLength - 7)}..";

				if(!overwrite && Directory.Exists(path)) {
					var pathNum = 1;
					while(Directory.Exists(path + $" ({pathNum})"))
						pathNum++;

					path += $" ({pathNum})";
				}

				if(!Directory.Exists(path))
					Directory.CreateDirectory(path);

				foreach(var e in files) {
					var entryPath = Path.Combine(path, e.Key);
					if(overwrite || !File.Exists(entryPath)) {
						using(var s = File.OpenWrite(entryPath)) {
							e.Value.stream.CopyTo(s);
							s.SetLength(s.Position);
						}
					}

					progressCb((float)++progress / steps);
				}
			} finally {
				foreach(var item in files)
					Marshal.FreeHGlobal(item.Value.ptr);
			}
		}
	}
}
