using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Kurento.NET;
using Microsoft.Extensions.Logging.Console;
using Microsoft.MixedReality.WebRTC;
using IceCandidate = Microsoft.MixedReality.WebRTC.IceCandidate;

namespace ConsoleApp1 {
	internal class Program {
		private static int numFrames;
		private static WebRtcEndpoint webRtcEndpoint;
		private static PeerConnection peerConnection;
		private static Transceiver audioTransceiver;
		private static Queue<IceCandidate> kurentoIceCandidates = new Queue<IceCandidate>();
		private static bool kurentoGathered;
		private static bool kurentoReady;
		private static bool clientReady;

		public static async Task Main(string[] args) {
			MyLogger logger = new MyLogger();
			var kurentoClient = new KurentoClient("ws://hive.ru:8888/kurento");//, logger);
			Console.WriteLine("Kurento <- Getting server info");
			var serverManager = kurentoClient.GetServerManager();
			var serverInfo = await serverManager.GetInfoAsync();
			Console.WriteLine($"Kurento -> server version: {serverInfo.version}");
			
			Console.WriteLine("Kurento <- Creating MediaPipeline...");
			var pipeLine = await kurentoClient.CreateAsync(new MediaPipeline());
			Console.WriteLine($"Kurento -> Pipeline created: {pipeLine.id}");
			Console.WriteLine("Kurento <- Creating WebRTC Endpoint...");
			webRtcEndpoint = await kurentoClient.CreateAsync(new WebRtcEndpoint(pipeLine));
			Console.WriteLine($"Kurento -> WebRTC Endpoint created: {webRtcEndpoint.id}");
			webRtcEndpoint.OnIceComponentStateChanged += WebRtcEndpointOnIceComponentStateChanged;
			webRtcEndpoint.IceCandidateFound += WebRtcEndpointOnIceCandidateFound;
			webRtcEndpoint.IceGatheringDone += WebRtcEndpointOnIceGatheringDone;
			webRtcEndpoint.NewCandidatePairSelected += WebRtcEndpointOnNewCandidatePairSelected;
			
			// дока на метод Connect
			// https://doc-kurento.readthedocs.io/en/stable/_static/client-javadoc/org/kurento/client/MediaElement.html#connect-org.kurento.client.MediaElement-
			// Connects two elements, with the media flowing from left to right
			Console.WriteLine("Kurento <- Connecting WebRTC Endpoint to itself");
			await webRtcEndpoint.ConnectAsync(webRtcEndpoint, MediaType.AUDIO);
			Console.WriteLine("Kurento -> WebRTC Endpoint connected to itself");

			Console.WriteLine($"Client: Creating PeerConnection...");

			peerConnection = new PeerConnection();

			peerConnection.Connected += PeerConnectionOnConnected;
			peerConnection.LocalSdpReadytoSend += PeerConnectionOnLocalSdpReadytoSend;
			peerConnection.IceStateChanged += PeerConnectionOnIceStateChanged;
			peerConnection.AudioTrackAdded += PeerConnectionOnAudioTrackAdded;
			peerConnection.IceGatheringStateChanged += PeerConnectionOnIceGatheringStateChanged;
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

			Console.WriteLine($"Client: peerConnection.Initialized = {peerConnection.Initialized}");

			var microphoneConfig = new LocalAudioDeviceInitConfig {
				AutoGainControl = true
			};

			var microphoneSource = await DeviceAudioTrackSource.CreateAsync(microphoneConfig);
			Console.WriteLine("Client: Microphone detected");

			var localTrackConfig = new LocalAudioTrackInitConfig {
				trackName = "microphone_track"
			};

			var localAudioTrack = LocalAudioTrack.CreateFromSource(microphoneSource, localTrackConfig);

			audioTransceiver = peerConnection.AddTransceiver(MediaKind.Audio);

			audioTransceiver.LocalAudioTrack = localAudioTrack;
			audioTransceiver.DesiredDirection = Transceiver.Direction.SendReceive;

			Console.WriteLine("Client: AudioTransceiver configured with localAudioTrack");

			var offerCreated = peerConnection.CreateOffer();
			Console.WriteLine($"Client: SDP offer created = {offerCreated}");

			while (!kurentoGathered) {
				Thread.Sleep(100);
			}
			Console.WriteLine("Client: Dequeuing kurento ICE candidates...");

			while (kurentoIceCandidates.Count > 0) {
				var kurentoIceCandidate = kurentoIceCandidates.Dequeue();
				Console.WriteLine($"Client: Adding Ice candidate: {kurentoIceCandidate.Content}");
				peerConnection.AddIceCandidate(kurentoIceCandidate);
				Console.WriteLine("Client: Ice candidate added");
			}

			while (!clientReady || !kurentoReady) {
				Thread.Sleep(100);
			}
			
			Console.WriteLine("Web RTC connection established!");
			
			

			Console.ReadKey(true);

			localAudioTrack?.Dispose();
			microphoneSource?.Dispose();
			peerConnection.Dispose();
		}

		#region Kurento peer event handlers

		private static void WebRtcEndpointOnIceComponentStateChanged(OnIceComponentStateChangedEventArgs args) {
			Console.WriteLine($"Kurento -> IceComponentState changed to {args.state}. ({args.componentId}:{args.streamId})");
			if (args.state == IceComponentState.READY) {
				kurentoReady = true;
			}

		}

		private static void WebRtcEndpointOnIceCandidateFound(IceCandidateFoundEventArgs args) {
			Console.WriteLine("Kurento -> Ice candidate found:");
			var kurentoFormattedCandidate = args.candidate;
			Console.WriteLine(kurentoFormattedCandidate.candidate);

			var iceCandidate = new IceCandidate {
				Content = kurentoFormattedCandidate.candidate,
				SdpMid = kurentoFormattedCandidate.sdpMid,
				SdpMlineIndex = kurentoFormattedCandidate.sdpMLineIndex
			};

			kurentoIceCandidates.Enqueue(iceCandidate);
			Console.WriteLine("Client: Kurento Ice candidate enqueued.");
			// Thread.Sleep(5000);
			// Console.WriteLine("Client: Kurento Ice candidate adding...");
			// peerConnection.AddIceCandidate(iceCandidate);
			// Console.WriteLine("Client: Kurento Ice candidate added.");
		}

		private static void WebRtcEndpointOnIceGatheringDone(IceGatheringDoneEventArgs args) {
			Console.WriteLine("Kurento -> Ice gathering done.");
			kurentoGathered = true;
		}

		private static void WebRtcEndpointOnNewCandidatePairSelected(NewCandidatePairSelectedEventArgs args) {
			Console.WriteLine($"Kurento -> New ICE candidate pair selected: {args.candidatePair.localCandidate} <-> {args.candidatePair.localCandidate})");
		}

		#endregion

		#region Client peer event handlers

		private static void PeerConnectionOnIceGatheringStateChanged(IceGatheringState newState) {
			Console.WriteLine(value: $"Client: ICE candidates gathering state: {newState}");
		}

		private static void PeerConnectionOnIceCandidateReadytoSend(IceCandidate clientIceCandidate) {
			Console.WriteLine("Client: client ICE candidate found:");
			Console.WriteLine(clientIceCandidate.Content);
			var kurentoFormattedIceCandidate = new Kurento.NET.IceCandidate {
				candidate = clientIceCandidate.Content,
				sdpMid = clientIceCandidate.SdpMid,
				sdpMLineIndex = clientIceCandidate.SdpMlineIndex
			};
			Console.WriteLine("Kurento <- Adding client ICE candidate...");
			webRtcEndpoint.AddIceCandidateAsync(kurentoFormattedIceCandidate).Wait();
			Console.WriteLine("Kurento -> client ICE candidate added");
		}

		private static void PeerConnectionOnLocalSdpReadytoSend(SdpMessage message) {
			Console.WriteLine("Client: Local SDP offer:");
			Console.WriteLine(message.Content);
			Console.WriteLine("Kurento <- Sending client SDP offer...");
			var sdpAnswerAsync = webRtcEndpoint.ProcessOfferAsync(message.Content);
			sdpAnswerAsync.Wait();
			Console.WriteLine("Client: SDP answer received:");
			var sdpAnswer = sdpAnswerAsync.Result;
			Console.WriteLine(sdpAnswer);
			Console.WriteLine("Client: Applying answer to local peerConnection...");
			var sdpAnswerMessage = new SdpMessage() {
				Content = sdpAnswer,
				Type = SdpMessageType.Answer
			};
			var remoteDescriptionAsync = peerConnection.SetRemoteDescriptionAsync(sdpAnswerMessage);
			remoteDescriptionAsync.Wait();
			Console.WriteLine("Client: SDP answer accepted");
			Console.WriteLine($"Client: Remote track detected: {audioTransceiver.RemoteTrack.Name}");

			
			Console.WriteLine("Kurento <- Starting ICE candidates gathering...");
			webRtcEndpoint.GatherCandidatesAsync().Wait();
			Console.WriteLine("Kurento -> Started ICE candidates gathering");
			
		}

		private static void PeerConnectionOnAudioTrackAdded(RemoteAudioTrack remoteTrack) {
			remoteTrack.AudioFrameReady += RemoteTrackOnAudioFrameReady;
		}

		private static void RemoteTrackOnAudioFrameReady(AudioFrame frame) {
			if (!clientReady || !kurentoReady) return;
			++numFrames;
			// var bytes = new byte[frame.sampleCount*2];
			// Marshal.Copy(frame.audioData, bytes, 0, bytes.Length);
			// var maxByte = 0;
			// foreach (var b in bytes) {
			// 	maxByte = Math.Max(maxByte, b);
			// }
			// Console.WriteLine(maxByte);
			if (numFrames % 100 == 0) {
				Console.WriteLine($"Client: Received audio frames: {numFrames}");
			}
		}

		private static void PeerConnectionOnConnected() {
			Console.WriteLine("Client: peerConnection connected.");
		}

		private static void PeerConnectionOnIceStateChanged(IceConnectionState newState) {
			Console.WriteLine($"Client: ICE state: {newState}");
			if (newState == IceConnectionState.Completed) {
				clientReady = true;
			}
		}

		#endregion

	}

}