using System;
using System.Security.Authentication;
using System.Text;
using Unity.WebRTC;
using UnityEngine;
using WebSocketSharp;

namespace Unity.RenderStreaming
{
    public class WebSocketSignaling : ISignaling
    {
        private readonly string url;

        private bool running;
        private WebSocket webSocket;

        public WebSocketSignaling(string url)
        {
            this.url = url;
        }

        private void WebSocketOnClose(object sender, CloseEventArgs e)
        {
            Debug.LogError($"Signaling: WS connection closed, code: {e.Code}");
        }

        private void WebSocketOnError(object sender, ErrorEventArgs e)
        {
            Debug.LogError($"Signaling: WS connection error: {e.Message}");
        }

        private void WebSocketOnMessage(object sender, MessageEventArgs e)
        {
            var content = Encoding.UTF8.GetString(e.RawData);
            Debug.Log($"Signaling: Receiving message: {content}");

            try
            {
                var routedMessage = JsonUtility.FromJson<RoutedMessage<SignalingMessage>>(content);

                SignalingMessage msg;
                if (!string.IsNullOrEmpty(routedMessage.from))
                {
                    msg = routedMessage.message;
                }
                else
                {
                    msg = JsonUtility.FromJson<SignalingMessage>(content);
                }

                if (!string.IsNullOrEmpty(msg.type))
                {
                    if (msg.type == "signIn")
                    {
                        if (msg.status == "SUCCESS")
                        {
                            // this._connectionId = msg.connectionId;
                            // this._sessionId = msg.peerId;
                            // Debug.Log("Signaling: Slot signed in.");
                            //
                            // this.WSSend(
                            //     "{\"type\":\"furioos\",\"task\":\"enableStreaming\",\"streamTypes\":\"WebRTC\",\"controlType\":\"RenderStreaming\"}");
                            //
                            // OnSignedIn?.Invoke(this);
                        }
                        else
                        {
                            Debug.LogError("Signaling: Sign-in error : " + msg.message);
                        }
                    }
                    else if (msg.type == "reconnect")
                    {
                        if (msg.status == "SUCCESS")
                        {
                            Debug.Log("Signaling: Slot reconnected.");
                        }
                        else
                        {
                            Debug.LogError("Signaling: Reconnect error : " + msg.message);
                        }
                    }

                    if (msg.type == "offer")
                    {
                        if (!string.IsNullOrEmpty(routedMessage.from))
                        {
                            var offer = new OfferResData {connectionId = routedMessage.@from, sdp = msg.sdp};
                            OnOffer?.Invoke(offer);
                        }
                        else
                        {
                            Debug.LogError("Signaling: Received message from unknown peer");
                        }
                    }
                }
                else if (!string.IsNullOrEmpty(msg.candidate))
                {
                    if (!string.IsNullOrEmpty(routedMessage.from))
                    {
                        var candidate = new CandidateResData
                        {
                            connectionId = routedMessage.@from,
                            candidate = msg.candidate,
                            sdpMLineIndex = msg.sdpMLineIndex,
                            sdpMid = msg.sdpMid
                        };

                        OnIceCandidate?.Invoke(candidate);
                    }
                    else
                    {
                        Debug.LogError("Signaling: Received message from unknown peer");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("Signaling: Failed to parse message: " + ex);
            }
        }

        private void WebSocketOnOpen(object sender, EventArgs e)
        {
            Debug.Log("Signaling: WS connected.");
        }

        public void Start()
        {
            running = true;

            if (webSocket == null)
            {
                webSocket = new WebSocket(url);
                webSocket.SslConfiguration.EnabledSslProtocols =
                    SslProtocols.Tls12 | SslProtocols.Tls11 | SslProtocols.Tls;
                webSocket.OnOpen += WebSocketOnOpen;
                webSocket.OnMessage += WebSocketOnMessage;
                webSocket.OnError += WebSocketOnError;
                webSocket.OnClose += WebSocketOnClose;
                webSocket.ConnectAsync();
            }
        }

        public void Stop()
        {
            running = false;
            webSocket?.Close();
            webSocket = null;
        }

        public void SendCandidate(string connectionId, RTCIceCandidate candidate)
        {
            var data = new CandidateResData
            {
                connectionId = connectionId,
                candidate = candidate.candidate,
                sdpMLineIndex = candidate.sdpMLineIndex,
                sdpMid = candidate.sdpMid
            };

            var routedMessage = new RoutedMessage<CandidateResData> {to = connectionId, message = data};
            Send(routedMessage);
        }

        public void SendAnswer(string connectionId, RTCPeerConnection peerConnection)
        {
            RTCAnswerOptions options = default;
            var op = peerConnection.CreateAnswer(ref options);
            while (op.MoveNext())
            {

            }

            if (op.isError)
            {
                Debug.LogError($"Network Error: {op.error}");
            }

            var op2 = peerConnection.SetLocalDescription(ref op.desc);

            while (op2.MoveNext())
            {

            }

            if (op2.isError)
            {
                Debug.LogError($"Network Error: {op2.error}");
            }

            var answer = new Signaling.AnswerReqData {connectionId = connectionId, sdp = op.desc.sdp};
            var routedMessage = new RoutedMessage<Signaling.AnswerReqData>{to = connectionId, message = answer};

            Send(routedMessage);
        }

        private void Send(object data)
        {
            if (this.webSocket == null || this.webSocket.ReadyState != WebSocketState.Open)
            {
                Debug.LogError("Signaling: WS is not connected. Unable to send message");
                return;
            }

            if (data is string s)
            {
                Debug.Log("Signaling: Sending WS data: " + s);
                this.webSocket.Send(s);
            }
            else
            {
                string str = JsonUtility.ToJson(data);
                Debug.Log("Signaling: Sending WS data: " + str);
                this.webSocket.Send(str);
            }
        }

        public event OnOfferEventHandler OnOffer;
        public event OnIceCandidateEventHandler OnIceCandidate;
    }
}
