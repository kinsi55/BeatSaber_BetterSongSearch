using System;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace BetterSongSearch.Util {
	static class UnityWebrequestWrapper {
		public static readonly string UserAgent = "BetterSongSearch/" + Assembly.GetExecutingAssembly().GetName().Version.ToString(3);

		public static async Task<bool> Download(string url, DownloadHandler handler, CancellationToken token = default, Action<float> progressCb = null, UnityWebRequest uwr = null) {
			var www = uwr ?? UnityWebRequest.Get(url);

			try {
				www.SetRequestHeader("User-Agent", UserAgent);
				if(handler != null)
					www.downloadHandler = handler;
				if(uwr == null)
					www.disposeDownloadHandlerOnDispose = false;

				var req = www.SendWebRequest();

				var lastState = 0f;
				var timeouter = new System.Diagnostics.Stopwatch();
				timeouter.Start();

				while(!req.isDone) {
					if(token.IsCancellationRequested) {
						www.Abort();
						throw new TaskCanceledException();
					}

					await Task.Delay(20);

					if(lastState == www.downloadProgress) {
						if(timeouter.ElapsedMilliseconds < (lastState == 0 ? 6000 : 10000))
							continue;

						www.Abort();
						throw new TimeoutException();
					}

					lastState = www.downloadProgress;

					if(progressCb != null && lastState > 0)
						progressCb(lastState);

					timeouter.Restart();
				}

				var successful = www.isDone && www.result == UnityWebRequest.Result.Success;
				if(!successful)
					Plugin.Log.Warn($"UnityWebRequest failed: result={www.result}, status={www.responseCode}, error={www.error}, url={url}");
				return successful;
			} finally {
				if(www != null && uwr == null)
					www.Dispose();
			}
		}

		public static async Task<byte[]> DownloadBytes(string url, CancellationToken token = default, Action<float> progressCb = null) {
			using(var dhb = new DownloadHandlerBuffer()) {
				if(await Download(url, dhb, token, progressCb))
					return dhb.data;
			}

			token.ThrowIfCancellationRequested();
			Plugin.Log.Warn($"Retrying through direct WinHTTP: {url}");
			var bytes = await Task.Run(() => NativeHttpDownloader.Download(url), token);
			Plugin.Log.Info($"Direct WinHTTP downloaded {bytes.Length} bytes from {url}");
			return bytes;
		}

		public static async Task<string> DownloadText(string url, CancellationToken token = default, Action<float> progressCb = null) {
			return Encoding.UTF8.GetString(await DownloadBytes(url, token, progressCb));
		}

		public static async Task<Sprite> DownloadSprite(string url, CancellationToken token = default, Action<float> progressCb = null) {
			using(var dhb = new DownloadHandlerTexture()) {
				Texture2D t;
				if(await Download(url, dhb, token, progressCb)) {
					t = dhb.texture;
				} else {
					token.ThrowIfCancellationRequested();
					Plugin.Log.Warn($"Retrying cover through direct WinHTTP: {url}");
					var bytes = await Task.Run(() => NativeHttpDownloader.Download(url), token);
					t = new Texture2D(2, 2);
					if(!t.LoadImage(bytes, true)) {
						UnityEngine.Object.Destroy(t);
						return null;
					}
					Plugin.Log.Info($"Direct WinHTTP loaded cover ({bytes.Length} bytes) from {url}");
				}

				t.wrapMode = TextureWrapMode.Clamp;
				return Sprite.Create(t, new Rect(0, 0, t.width, t.height), Vector3.zero, 100);
			}
		}

		public static async Task<AudioClip> DownloadAudio(string url, CancellationToken token = default, AudioType type = AudioType.UNKNOWN, Action<float> progressCb = null) {
			using(var www = UnityWebRequestMultimedia.GetAudioClip(url, type)) {
				if(await Download(url, null, token, progressCb, www))
					return DownloadHandlerAudioClip.GetContent(www);
			}

			token.ThrowIfCancellationRequested();
			Plugin.Log.Warn($"Retrying preview through direct WinHTTP: {url}");
			var bytes = await Task.Run(() => NativeHttpDownloader.Download(url), token);
			var tempPath = Path.Combine(Path.GetTempPath(), $"BetterSongSearch-{Guid.NewGuid():N}.mp3");
			try {
				await Task.Run(() => File.WriteAllBytes(tempPath, bytes), token);
				var localUrl = new Uri(tempPath).AbsoluteUri;
				using(var localRequest = UnityWebRequestMultimedia.GetAudioClip(localUrl, type)) {
					if(!await Download(localUrl, null, token, progressCb, localRequest))
						return null;

					var clip = DownloadHandlerAudioClip.GetContent(localRequest);
					Plugin.Log.Info($"Direct WinHTTP loaded preview ({bytes.Length} bytes) from {url}");
					return clip;
				}
			} finally {
				try {
					File.Delete(tempPath);
				} catch(Exception ex) {
					Plugin.Log.Debug($"Could not remove temporary preview {tempPath}: {ex.Message}");
				}
			}
		}
	}
}
