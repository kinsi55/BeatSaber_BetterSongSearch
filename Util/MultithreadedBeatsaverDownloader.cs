using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace BetterSongSearch.Util {
	class MultithreadedBeatsaverDownloader {
        const int BATCHSIZE = 1048576;
        readonly HttpClient client;
        readonly string url;
        Action<float> progressCb;

        public MultithreadedBeatsaverDownloader(HttpClient client, string url, Action<float> progressCb) {
            this.client = client;
            this.url = url;
            this.progressCb = progressCb;
        }

        bool isDownloadingFromCDN = false;
        int downloadSize = 0;
        int downloadedBytes = 0;
        float progress = 0f;
        byte[] fileOut;
        CancellationToken token;

        void Reset() {
            progress = 0f;
            downloadedBytes = 0;
		}

        void AddDownloadedBytes(int bytes) {
            Interlocked.Add(ref downloadedBytes, bytes);

            var newProgress = (float)downloadedBytes / downloadSize;

            if(newProgress - progress > 0.01f) {
                progress = newProgress;

                progressCb(progress);
            }
        }

        async Task<Task> DownloadRange(int start, int length) {
            var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Add("range", $"bytes={start}-{start + length - 1}");

            HttpResponseMessage resp = null;

            void cleanup(Exception ex = null) {
                Plugin.Log.Debug(string.Format("[{0}-{1}] Cleanup: {2}", start, start + length, ex));

                resp?.Dispose();
                req.Dispose();

                if(ex != null)
                    throw ex;
            }

            try {
                Plugin.Log.Debug(string.Format("Opening connection for {0}-{1}", start, start + length));
                resp = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, token);
                Plugin.Log.Debug(string.Format("[{0}-{1}] Opened connection: {2}", start, start + length, resp.StatusCode));

                if((int)resp.StatusCode == 429 || resp.ReasonPhrase == "Too Many Requests")
                    throw new Exception("Ratelimited, retry later");

                if(resp.StatusCode != HttpStatusCode.OK && resp.StatusCode != HttpStatusCode.PartialContent)
                    throw new Exception($"Unexpected HTTP response: {resp.StatusCode} {resp.ReasonPhrase}");

                int len = (int)(resp.Content.Headers.ContentLength ?? 0);

                if(len == 0)
                    throw new Exception("Response has no length");

                int end = len + start;

                return Task.Run(async () => {
                    try {
                        using(var stream = await resp.Content.ReadAsStreamAsync()) {
                            if(fileOut == null && start == 0) {
                                downloadSize = len;

                                // If we got a partial response (Requested bytes are less than the total fizesize) get the total fizesize from resp header
                                if(resp.StatusCode == HttpStatusCode.PartialContent)
                                    downloadSize = (int)resp.Content.Headers.ContentRange.Length;

                                fileOut = new byte[downloadSize];

                                foreach(var x in resp.Headers) {
                                    if(x.Key.ToLower() == "cf-cache-status") {
                                        isDownloadingFromCDN = x.Value.FirstOrDefault() == "HIT";
                                        break;
									}
                                }

                                Plugin.Log.Debug(string.Format("downloadSize: {0}, isDownloadingFromCache: {1}", downloadSize, isDownloadingFromCDN));
                            }

                            stream.ReadTimeout = 3000;

                            int pos = start;

                            var buf = new byte[2^14];
                            while(pos != end) {
                                if(token.IsCancellationRequested)
                                    throw new TaskCanceledException();

                                var read = await stream.ReadAsync(buf, 0, buf.Length, token);
                                if(read == 0)
                                    break;

                                Buffer.BlockCopy(buf, 0, fileOut, pos, read); // Faster on Mono than Array.Copy apparently

                                pos += read;

                                AddDownloadedBytes(read);
                            }

                            Plugin.Log.Debug(string.Format("[{0}-{1}] Downloaded {2} bytes ({3} left)", start, start + length, pos, end - pos));

                            if(pos != end)
                                throw new Exception("Response was incomplete");
                        }
                    } catch(Exception ex) {
                        cleanup(ex);
                    }
                });
            } catch(Exception ex) {
                // Gotta do this manually here, else C# will yell at us because it has no idea cleanup will throw
                cleanup();
                throw ex;
            }
        }


        public async Task<byte[]> Load(CancellationToken token) {
            this.token = token;

            try {
                await (await DownloadRange(0, BATCHSIZE));

                var leftover = downloadSize - downloadedBytes;

                if(leftover > 0) {
                    // Chunks should be at least 3M in size
                    var connections = !isDownloadingFromCDN ? 1 : (int)Math.Floor(Mathf.Clamp(leftover / (BATCHSIZE * 3f), 1, 3));
                    var bytesPerConnection = (int)Math.Floor((float)leftover / connections);
                    var offs = downloadedBytes;

                    Plugin.Log.Debug(string.Format("Downloading song with {0} connection(s)", connections));

                    // Open connections in parallel
                    var connectingRequests = new List<Task<Task>>() { };

                    while(connections > 0) {
                        connections--;

                        var chunkSize = connections == 0 ? downloadSize - offs : bytesPerConnection;

                        Plugin.Log.Debug(string.Format("Chunk {0}, size {1}, start {2}", connections, chunkSize, offs));

                        connectingRequests.Add(DownloadRange(offs, chunkSize));

                        offs += bytesPerConnection;
                    }

                    Plugin.Log.Debug("Waiting for all connections to open...");
                    // Wait for all connections to open
                    await Task.WhenAll(connectingRequests);
                    Plugin.Log.Debug("Waiting for all chunks to download...");
                    // Now that all connections exist wait for them to finish downloading their chunk
                    await Task.WhenAll(connectingRequests.Select(x => x.Result));
                }

                return fileOut;
            } catch(Exception ex) {
                throw ex;
			}
        }
	}
}
