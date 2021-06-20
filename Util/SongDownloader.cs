using BetterSongSearch.UI;
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
using System.Threading;
using System.Threading.Tasks;
using static BetterSongSearch.UI.DownloadHistoryView;

namespace BetterSongSearch.Util {
	static class SongDownloader {
		private static HttpClient client = null;

		static void InitClientIfNecessary() {
			if(client != null)
				return;

			client = new HttpClient(new HttpClientHandler() {
				AutomaticDecompression = DecompressionMethods.GZip,
				AllowAutoRedirect = false,
				//Proxy = new WebProxy("localhost:8888")
			});

			client.DefaultRequestHeaders.Add("User-Agent", "BetterSongSearch/" + Assembly.GetExecutingAssembly().GetName().Version.ToString(3));
			client.Timeout = TimeSpan.FromSeconds(10);
		}

		public static async Task<string> GetSongDescription(string key, CancellationToken token) {
			InitClientIfNecessary();

			using(var resp = await client.GetAsync($"https://beatsaver.com/api/maps/detail/{key.ToLower()}", HttpCompletionOption.ResponseHeadersRead, token)) {
				if(resp.StatusCode != HttpStatusCode.OK)
					throw new Exception($"Unexpected HTTP response: {resp.StatusCode} {resp.ReasonPhrase}");

				using(var reader = new StreamReader(await resp.Content.ReadAsStreamAsync()))
				using(var jsonReader = new JsonTextReader(reader)) {
					JsonSerializer ser = new JsonSerializer();

					return ser.Deserialize<JObject>(jsonReader).GetValue("description").Value<string>();
				}
			}
		}

		public static async Task BeatmapDownload(DownloadHistoryEntry entry, CancellationToken token, Action<float> progressCb) {
			InitClientIfNecessary();

			var folderName = $"{entry.key} ({entry.songName} - {entry.levelAuthorName})";

			var dl = new MultithreadedBeatsaverDownloader(client, $"https://beatsaver.com/cdn/{entry.key}/{entry.hash}.zip".ToLower(), (p) => {
				entry.status = DownloadHistoryEntry.DownloadStatus.Downloading;
				progressCb(p);
			});
			byte[] res;
			var t = new CancellationTokenSource();
			token.Register(t.Cancel);
			try {
				res = await dl.Load(t.Token);
			} catch(Exception ex) {
				t.Cancel();
				throw ex;
			}

			using(var s = new MemoryStream(res)) {
				entry.status = DownloadHistoryEntry.DownloadStatus.Extracting;
				progressCb(0);

				// Not async'ing this as BeatmapDownload() is supposed to be called in a task
				ExtractZip(s, folderName, t.Token, progressCb);
			}
		}

		static void ExtractZip(Stream zipStream, string basePath, CancellationToken token, Action<float> progressCb, bool overwrite = false) {
			string path = Path.Combine(CustomLevelPathHelper.customLevelsDirectoryPath, string.Concat(basePath.Split(Path.GetInvalidFileNameChars())).Trim());

			if(!overwrite && Directory.Exists(path)) {
				int pathNum = 1;
				while(Directory.Exists(path + $" ({pathNum})"))
					pathNum++;

				path += $" ({pathNum})";
			}

			int steps;
			int progress = 0;
			Dictionary<string, byte[]> files;

			// Unzip everything to memory first so we dont end up writing half a song incase something breaks
			using(var archive = new ZipArchive(zipStream, ZipArchiveMode.Read)) {
				var buf = new byte[2 ^ 15];

				using(var ms = new MemoryStream()) {
					steps = archive.Entries.Count() * 2;
					files = new Dictionary<string, byte[]>(steps);

					foreach(var entry in archive.Entries) {
						using(var str = entry.Open()) {
							for(; ; ) {
								if(token.IsCancellationRequested)
									throw new TaskCanceledException();

								int read = str.Read(buf, 0, buf.Length);
								if(read == 0)
									break;

								ms.Write(buf, 0, read);
							}

							files.Add(entry.Name, ms.ToArray());
						}
						progressCb((float)++progress / steps);
						ms.SetLength(0);
					}
				}
			}

			if(token.IsCancellationRequested)
				throw new TaskCanceledException();

			if(!Directory.Exists(path))
				Directory.CreateDirectory(path);

			foreach(var e in files) {
				var entryPath = Path.Combine(path, e.Key);
				if(overwrite || !File.Exists(entryPath))
					File.WriteAllBytes(entryPath, e.Value);

				progressCb((float)++progress / steps);

				// Dont think cancelling here is smart, might as well finish writing this song to not have a corrupted download
				// if(token.IsCancellationRequested)
				//     throw new TaskCanceledException();
			}
		}
	}
}
