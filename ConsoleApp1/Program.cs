using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Kurento.NET;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Microsoft.MixedReality.WebRTC;
using IceCandidate = Microsoft.MixedReality.WebRTC.IceCandidate;

namespace ConsoleApp1 {
	internal class Program {
		private static int numFrames;
		private static WebRtcEndpoint webRtcEndpoint;
		private static PeerConnection peerConnection;
		private static Transceiver audioTransceiver;

		public static async Task Main(string[] args) {
			
			
			Console.WriteLine("Kurento side adjustment");
			MyLogger logger = new MyLogger();
			var kurentoClient = new KurentoClient("ws://hive.ru:8888/kurento");//, logger);
			Console.WriteLine("Getting kurento server info");
			var serverInfo = await kurentoClient.GetServerManager().GetInfoAsync();
			Console.WriteLine($"Krento server version: {serverInfo.version}");

			Console.WriteLine("Creating MediaPipeline");
			var pipeLine = await kurentoClient.CreateAsync(new MediaPipeline());
			Console.WriteLine($"Pipeline created: {pipeLine.id}");
			Console.WriteLine("Creating WebRTC Endpoint on Kurento");
			webRtcEndpoint = await kurentoClient.CreateAsync(new WebRtcEndpoint(pipeLine));
			Console.WriteLine($"WebRTC Endpoint created: {webRtcEndpoint.id}");

			
			// дока на метод Connect
			// https://doc-kurento.readthedocs.io/en/stable/_static/client-javadoc/org/kurento/client/MediaElement.html#connect-org.kurento.client.MediaElement-
			// Connects two elements, with the media flowing from left to right
			Console.WriteLine("Connecting WebRTC Endpoint to itself");
			await webRtcEndpoint.ConnectAsync(webRtcEndpoint, MediaType.AUDIO);
			Console.WriteLine("WebRTC Endpoint connected to itself");
			


			Console.WriteLine("\n-------------------------------------------------------\n");

			Console.WriteLine("Client side adjustment");
			Console.WriteLine($"Creating PeerConnection");

			peerConnection = new PeerConnection();

			peerConnection.Connected += PeerConnectionOnConnected;
			peerConnection.LocalSdpReadytoSend += PeerConnectionOnLocalSdpReadytoSend;
			peerConnection.IceStateChanged += PeerConnectionOnIceStateChanged;
			peerConnection.AudioTrackAdded += PeerConnectionOnAudioTrackAdded;
			peerConnection.IceCandidateReadytoSend += PeerConnectionOnIceCandidateReadytoSend;

			var connectionConfig = new PeerConnectionConfiguration {
				IceServers = new List<IceServer> {
					new IceServer {
						Urls = {"turn:hive.ru:3478"},
						TurnUserName = "hive",
						TurnPassword = "41414242"
					}
				}
			};

			await peerConnection.InitializeAsync(connectionConfig);

			Console.WriteLine($"peerConnection.Initialized = {peerConnection.Initialized}");

			var microphoneConfig = new LocalAudioDeviceInitConfig {
				AutoGainControl = true
			};

			var microphoneSource = await DeviceAudioTrackSource.CreateAsync(microphoneConfig);
			Console.WriteLine("Microphone detected");

			var localTrackConfig = new LocalAudioTrackInitConfig {
				trackName = "microphone_track"
			};

			var localAudioTrack = LocalAudioTrack.CreateFromSource(microphoneSource, localTrackConfig);

			audioTransceiver = peerConnection.AddTransceiver(MediaKind.Audio);

			audioTransceiver.LocalAudioTrack = localAudioTrack;
			audioTransceiver.DesiredDirection = Transceiver.Direction.SendReceive;

			Console.WriteLine("AudioTransceiver configured with localAudioTrack");

			var offerCreated = peerConnection.CreateOffer();
			Console.WriteLine($"SDP offer created = {offerCreated}");

			Console.ReadKey(true);
			
			localAudioTrack?.Dispose();
			microphoneSource?.Dispose();
			peerConnection.Dispose();
		}

		private static void PeerConnectionOnIceCandidateReadytoSend(IceCandidate candidate) {
			Console.WriteLine("PeerConnectionOnIceCandidateReadytoSend:");
			Console.WriteLine(candidate.Content);
			
		}

		private static void PeerConnectionOnLocalSdpReadytoSend(SdpMessage message) {
			Console.WriteLine("Local SDP offer:");
			Console.WriteLine(message.Content);
			Console.WriteLine("Sending offer to Kurento...");
			var sdpAnswerAsync = webRtcEndpoint.ProcessOfferAsync(message.Content);
			sdpAnswerAsync.Wait();
			Console.WriteLine("SDP Answer received:");
			var sdpAnswer = sdpAnswerAsync.Result;
			Console.WriteLine(sdpAnswer);
			Console.WriteLine("Applying answer to local peerConnection");
			var sdpAnswerMessage = new SdpMessage() {
				Content = sdpAnswer,
				Type = SdpMessageType.Answer
			};
			var remoteDescriptionAsync = peerConnection.SetRemoteDescriptionAsync(sdpAnswerMessage);
			remoteDescriptionAsync.Wait();
			Console.WriteLine("SDP Answer accepted");
			Console.WriteLine($"Remote track detected: {audioTransceiver.RemoteTrack.Name}");
		}

		private static void PeerConnectionOnAudioTrackAdded(RemoteAudioTrack remoteTrack) {
			remoteTrack.AudioFrameReady += RemoteTrackOnAudioFrameReady;
		}

		private static void RemoteTrackOnAudioFrameReady(AudioFrame frame) {
			++numFrames;
			// var bytes = new byte[frame.sampleCount*2];
			// Marshal.Copy(frame.audioData, bytes, 0, bytes.Length);
			// var maxByte = 0;
			// foreach (var b in bytes) {
			// 	maxByte = Math.Max(maxByte, b);
			// }
			// Console.WriteLine(maxByte);
			if (numFrames % 100 == 0) {
				Console.WriteLine($"Received audio frames: {numFrames}");
			}
		}

		private static void PeerConnectionOnConnected() {
			Console.WriteLine("PeerConnection: connected.");
		}

		private static void PeerConnectionOnIceStateChanged(IceConnectionState newState) {
			Console.WriteLine($"ICE state: {newState}");
		}
	}

	public class MyLogger : ILogger {
		public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter) {
			var message = formatter(state, exception);

			if (!string.IsNullOrEmpty(message) || exception != null) {
				Console.WriteLine($"MyLogger: {message}");
			}
		}

		public bool IsEnabled(LogLevel logLevel) {
			return true;
		}

		public IDisposable BeginScope<TState>(TState state) {
			return null;
		}
	}

}