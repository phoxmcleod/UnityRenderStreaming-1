using System;
using System.Collections;
using Unity.WebRTC;
using UnityEngine;
using UnityEngine.Networking;

namespace Unity.RenderStreaming
{
    public class HttpSignaling : ISignaling
    {
        private readonly string url;
        private readonly MonoBehaviour parent;

        private bool running;
        private string sessionId;
        private long lastTimeGetOfferRequest = 0;
        private long lastTimeGetCandidateRequest = 0;

        public HttpSignaling(string url, MonoBehaviour parent)
        {
            this.url = url;
            this.parent = parent;
        }

        public void Start()
        {
            running = true;
            parent.StartCoroutine(LongPolling());
        }

        private IEnumerator LongPolling()
        {
            lastTimeGetOfferRequest = DateTime.UtcNow.Millisecond - 30000;
            lastTimeGetCandidateRequest = DateTime.UtcNow.Millisecond - 30000;

            yield return Create();

            if (string.IsNullOrEmpty(sessionId))
            {
                yield break;
            }

            while (running)
            {
                yield return GetOffer();
                yield return GetIceCandidate();
                yield return new WaitForSeconds(5); // wait interval time
            }
        }

        private IEnumerator Create()
        {
            var req = new UnityWebRequest($"{url}/signaling", "PUT");
            var op = req.SendWebRequest<NewResData>();
            yield return op;
            if (op.webRequest.isNetworkError)
            {
                Debug.LogError($"Network Error: {op.webRequest.error}");
                yield break;
            }

            var newResData = op.webRequest.DownloadHandlerJson<NewResData>().GetObject();
            sessionId = newResData.sessionId;
        }

        private IEnumerator GetOffer()
        {
            var req = new UnityWebRequest($"{url}/signaling/offer?fromtime={lastTimeGetOfferRequest}", "GET");
            req.SetRequestHeader("Session-Id", sessionId);
            var op = req.SendWebRequest<OfferResDataList>();
            yield return op;

            if (op.webRequest.isNetworkError)
            {
                Debug.LogError($"Network Error: {op.webRequest.error}");
                yield break;
            }
            var date = DateTimeExtension.ParseHttpDate(op.webRequest.GetResponseHeader("Date"));
            lastTimeGetOfferRequest = date.ToJsMilliseconds();

            var obj = op.webRequest.DownloadHandlerJson<OfferResDataList>().GetObject();
            if (obj == null)
            {
                yield break;
            }

            foreach (var offerResData in obj.offers)
            {
                OnOffer?.Invoke(offerResData);
            }
        }

        private IEnumerator GetIceCandidate()
        {
            var req = new UnityWebRequest($"{url}/signaling/candidate?fromtime={lastTimeGetCandidateRequest}", "GET");
            req.SetRequestHeader("Session-Id", sessionId);
            var op = req.SendWebRequest<CandidateContainerResDataList>();
            yield return op;

            if (op.webRequest.isNetworkError)
            {
                Debug.LogError($"Network Error: {op.webRequest.error}");
                yield break;
            }
            var date = DateTimeExtension.ParseHttpDate(op.webRequest.GetResponseHeader("Date"));
            lastTimeGetCandidateRequest = date.ToJsMilliseconds();

            var obj = op.webRequest.DownloadHandlerJson<CandidateContainerResDataList>().GetObject();
            if (obj == null)
            {
                yield break;
            }

            foreach (var container in obj.candidates)
            {
                foreach (var candidate in container.candidates)
                {
                    candidate.connectionId = container.connectionId;
                    OnIceCandidate?.Invoke(candidate);
                }
            }
        }

        public void Stop()
        {
            running = false;
        }

        public void SendCandidate(string connectionId, RTCIceCandidate candidate)
        {
            parent.StartCoroutine(PostCandidate(connectionId, candidate));
        }

        private IEnumerator PostCandidate(string connectionId, RTCIceCandidate candidate)
        {
            var obj = new Signaling.CandidateReqData
            {
                connectionId = connectionId,
                candidate = candidate.candidate,
                sdpMid = candidate.sdpMid,
                sdpMLineIndex = candidate.sdpMLineIndex
            };
            var data = new System.Text.UTF8Encoding().GetBytes(JsonUtility.ToJson(obj));
            var req = new UnityWebRequest($"{url}/signaling/candidate", "POST");
            req.SetRequestHeader("Session-Id", sessionId);
            req.SetRequestHeader("Content-Type", "application/json");
            req.uploadHandler = new UploadHandlerRaw(data);
            var op = req.SendWebRequest<None>();
            yield return op;

            if (op.webRequest.isNetworkError)
            {
                Debug.LogError($"Network Error: {op.webRequest.error}");
                yield break;
            }
        }

        public void SendAnswer(string connectionId, RTCPeerConnection peerConnection)
        {
            parent.StartCoroutine(PostAnswer(connectionId, peerConnection));
        }

        private IEnumerator PostAnswer(string connectionId, RTCPeerConnection peerConnection)
        {
            RTCAnswerOptions options = default;
            var op = peerConnection.CreateAnswer(ref options);
            yield return op;
            if (op.isError)
            {
                Debug.LogError($"Network Error: {op.error}");
                yield break;
            }

            var op2 = peerConnection.SetLocalDescription(ref op.desc);
            yield return op2;
            if (op2.isError)
            {
                Debug.LogError($"Network Error: {op2.error}");
                yield break;
            }

            var obj = new Signaling.AnswerReqData { connectionId = connectionId, sdp = op.desc.sdp };
            var data = new System.Text.UTF8Encoding().GetBytes(JsonUtility.ToJson(obj));
            var req = new UnityWebRequest($"{url}/signaling/answer", "POST");
            req.SetRequestHeader("Session-Id", sessionId);
            req.SetRequestHeader("Content-Type", "application/json");
            req.uploadHandler = new UploadHandlerRaw(data);
            var op3 = req.SendWebRequest<None>();

            yield return op3;
            if (op3.webRequest.isNetworkError)
            {
                Debug.LogError($"Network Error: {op3.webRequest.error}");
                yield break;
            }
        }

        public event OnOfferEventHandler OnOffer;
        public event OnIceCandidateEventHandler OnIceCandidate;
    }
}
