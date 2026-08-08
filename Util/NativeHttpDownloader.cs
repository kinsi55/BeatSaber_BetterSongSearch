using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;

namespace BetterSongSearch.Util {
	/// <summary>
	/// Direct WinHTTP fallback for Unity 6 installations where UnityWebRequest
	/// finishes with a transport error. It deliberately bypasses WinINET/WPAD
	/// proxy discovery and only supports HTTPS GETs used by BeatSaver.
	/// </summary>
	static class NativeHttpDownloader {
		// Beat Saber ships BSIPA's proxy as winhttp.dll beside the executable.
		// Load the Windows implementation explicitly so the fallback does not
		// resolve back into the same proxy that bootstraps IPA.
		const uint LOAD_LIBRARY_SEARCH_SYSTEM32 = 0x00000800;
		const uint WINHTTP_ACCESS_TYPE_NO_PROXY = 1;
		const uint WINHTTP_FLAG_SECURE = 0x00800000;
		const uint WINHTTP_QUERY_STATUS_CODE = 19;
		const uint WINHTTP_QUERY_FLAG_NUMBER = 0x20000000;
		static readonly IntPtr WinHttpModule = LoadSystemWinHttp();
		static readonly WinHttpOpenDelegate WinHttpOpen = LoadFunction<WinHttpOpenDelegate>("WinHttpOpen");
		static readonly WinHttpConnectDelegate WinHttpConnect = LoadFunction<WinHttpConnectDelegate>("WinHttpConnect");
		static readonly WinHttpOpenRequestDelegate WinHttpOpenRequest = LoadFunction<WinHttpOpenRequestDelegate>("WinHttpOpenRequest");
		static readonly WinHttpSendRequestDelegate WinHttpSendRequest = LoadFunction<WinHttpSendRequestDelegate>("WinHttpSendRequest");
		static readonly WinHttpReceiveResponseDelegate WinHttpReceiveResponse = LoadFunction<WinHttpReceiveResponseDelegate>("WinHttpReceiveResponse");
		static readonly WinHttpQueryHeadersDelegate WinHttpQueryHeaders = LoadFunction<WinHttpQueryHeadersDelegate>("WinHttpQueryHeaders");
		static readonly WinHttpQueryDataAvailableDelegate WinHttpQueryDataAvailable = LoadFunction<WinHttpQueryDataAvailableDelegate>("WinHttpQueryDataAvailable");
		static readonly WinHttpReadDataDelegate WinHttpReadData = LoadFunction<WinHttpReadDataDelegate>("WinHttpReadData");
		static readonly WinHttpCloseHandleDelegate WinHttpCloseHandle = LoadFunction<WinHttpCloseHandleDelegate>("WinHttpCloseHandle");

		public static byte[] Download(string url) {
			var uri = new Uri(url);
			if(!uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
				throw new NotSupportedException("Only HTTPS downloads are supported by the native fallback");

			IntPtr session = IntPtr.Zero;
			IntPtr connection = IntPtr.Zero;
			IntPtr request = IntPtr.Zero;
			try {
				session = WinHttpOpen(UnityWebrequestWrapper.UserAgent, WINHTTP_ACCESS_TYPE_NO_PROXY, null, null, 0);
				EnsureHandle(session, "WinHttpOpen");
				connection = WinHttpConnect(session, uri.Host, (ushort)uri.Port, 0);
				EnsureHandle(connection, "WinHttpConnect");
				request = WinHttpOpenRequest(connection, "GET", uri.PathAndQuery, null, null, IntPtr.Zero, WINHTTP_FLAG_SECURE);
				EnsureHandle(request, "WinHttpOpenRequest");

				if(!WinHttpSendRequest(request, null, 0, IntPtr.Zero, 0, 0, IntPtr.Zero))
					ThrowLastError("WinHttpSendRequest");
				if(!WinHttpReceiveResponse(request, IntPtr.Zero))
					ThrowLastError("WinHttpReceiveResponse");

				uint statusCode = 0;
				uint statusCodeSize = sizeof(uint);
				if(!WinHttpQueryHeaders(request, WINHTTP_QUERY_STATUS_CODE | WINHTTP_QUERY_FLAG_NUMBER, null, ref statusCode, ref statusCodeSize, IntPtr.Zero))
					ThrowLastError("WinHttpQueryHeaders");
				if(statusCode < 200 || statusCode >= 300)
					throw new InvalidDataException($"HTTP {statusCode} returned for {url}");

				using(var output = new MemoryStream()) {
					while(true) {
						if(!WinHttpQueryDataAvailable(request, out uint available))
							ThrowLastError("WinHttpQueryDataAvailable");
						if(available == 0)
							break;
						var buffer = new byte[(int)Math.Min(available, 1024u * 1024u)];
						if(!WinHttpReadData(request, buffer, (uint)buffer.Length, out uint read))
							ThrowLastError("WinHttpReadData");
						if(read == 0)
							break;
						output.Write(buffer, 0, (int)read);
					}
					return output.ToArray();
				}
			} finally {
				if(request != IntPtr.Zero) WinHttpCloseHandle(request);
				if(connection != IntPtr.Zero) WinHttpCloseHandle(connection);
				if(session != IntPtr.Zero) WinHttpCloseHandle(session);
			}
		}

		static void EnsureHandle(IntPtr handle, string operation) {
			if(handle == IntPtr.Zero)
				ThrowLastError(operation);
		}

		static void ThrowLastError(string operation) {
			var error = Marshal.GetLastWin32Error();
			throw new Win32Exception(error, $"{operation} failed with Win32 error {error}");
		}

		static IntPtr LoadSystemWinHttp() {
			var module = LoadLibraryEx("winhttp.dll", IntPtr.Zero, LOAD_LIBRARY_SEARCH_SYSTEM32);
			if(module == IntPtr.Zero)
				ThrowLastError("LoadLibraryEx(winhttp.dll)");
			return module;
		}

		static T LoadFunction<T>(string name) where T : class {
			var address = GetProcAddress(WinHttpModule, name);
			if(address == IntPtr.Zero)
				ThrowLastError($"GetProcAddress({name})");
			return (T)(object)Marshal.GetDelegateForFunctionPointer(address, typeof(T));
		}

		[UnmanagedFunctionPointer(CallingConvention.Winapi, CharSet = CharSet.Unicode, SetLastError = true)]
		delegate IntPtr WinHttpOpenDelegate(string userAgent, uint accessType, string proxyName, string proxyBypass, uint flags);
		[UnmanagedFunctionPointer(CallingConvention.Winapi, CharSet = CharSet.Unicode, SetLastError = true)]
		delegate IntPtr WinHttpConnectDelegate(IntPtr session, string serverName, ushort serverPort, uint reserved);
		[UnmanagedFunctionPointer(CallingConvention.Winapi, CharSet = CharSet.Unicode, SetLastError = true)]
		delegate IntPtr WinHttpOpenRequestDelegate(IntPtr connect, string verb, string objectName, string version, string referrer, IntPtr acceptTypes, uint flags);
		[UnmanagedFunctionPointer(CallingConvention.Winapi, CharSet = CharSet.Unicode, SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		delegate bool WinHttpSendRequestDelegate(IntPtr request, string headers, uint headersLength, IntPtr optional, uint optionalLength, uint totalLength, IntPtr context);
		[UnmanagedFunctionPointer(CallingConvention.Winapi, SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		delegate bool WinHttpReceiveResponseDelegate(IntPtr request, IntPtr reserved);
		[UnmanagedFunctionPointer(CallingConvention.Winapi, CharSet = CharSet.Unicode, SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		delegate bool WinHttpQueryHeadersDelegate(IntPtr request, uint infoLevel, string name, ref uint buffer, ref uint bufferLength, IntPtr index);
		[UnmanagedFunctionPointer(CallingConvention.Winapi, SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		delegate bool WinHttpQueryDataAvailableDelegate(IntPtr request, out uint available);
		[UnmanagedFunctionPointer(CallingConvention.Winapi, SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		delegate bool WinHttpReadDataDelegate(IntPtr request, byte[] buffer, uint bytesToRead, out uint bytesRead);
		[UnmanagedFunctionPointer(CallingConvention.Winapi, SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		delegate bool WinHttpCloseHandleDelegate(IntPtr handle);

		[DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
		static extern IntPtr LoadLibraryEx(string fileName, IntPtr file, uint flags);
		[DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
		static extern IntPtr GetProcAddress(IntPtr module, string procedureName);
	}
}
