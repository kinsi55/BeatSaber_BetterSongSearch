﻿using System;
using System.Collections.Generic;
using System.Linq;
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
				www.timeout = 10;
				www.disposeDownloadHandlerOnDispose = false;
				if(handler != null)
					www.downloadHandler = handler;

				var req = www.SendWebRequest();

				while(!req.isDone) {
					if(token.IsCancellationRequested) {
						www.Abort();
						break;
					}

					await Task.Delay(25);

					if(progressCb != null && www.downloadProgress > 0)
						progressCb(www.downloadProgress);
				}

				return www.isDone && !www.isHttpError && !www.isNetworkError;
			} catch {
				if(handler != null)
					handler.Dispose();
				throw;
			} finally {
				if(www != null && uwr == null)
					www.Dispose();
			}
		}

		public static async Task<byte[]> DownloadBytes(string url, CancellationToken token = default, Action<float> progressCb = null) {
			using(var dhb = new DownloadHandlerBuffer())
				return await Download(url, dhb, token, progressCb) ? dhb.data : null;
		}

		public static async Task<string> DownloadText(string url, CancellationToken token = default, Action<float> progressCb = null) {
			using(var dhb = new DownloadHandlerBuffer())
				return await Download(url, dhb, token, progressCb) ? dhb.text : null;
		}

		public static async Task<Sprite> DownloadSprite(string url, CancellationToken token = default, Action<float> progressCb = null) {
			var dhb = new DownloadHandlerTexture(true);
			
			try {
				if(!await Download(url, dhb, token, progressCb))
					return null;

				var t = dhb.texture;

				t.wrapMode = TextureWrapMode.Clamp;
				return Sprite.Create(t, new Rect(0, 0, t.width, t.height), Vector3.zero, 100);
			} finally {
				dhb.Dispose();
			}
		}

		public static async Task<AudioClip> DownloadAudio(string url, CancellationToken token = default, AudioType type = AudioType.UNKNOWN, Action<float> progressCb = null) {
			var www = UnityWebRequestMultimedia.GetAudioClip(url, type);
			
			try {
				if(!await Download(url, null, token, progressCb, www))
					return null;

				var clip = DownloadHandlerAudioClip.GetContent(www);

				return clip;
			} finally {
				www.downloadHandler.Dispose();
			}
		}
	}
}