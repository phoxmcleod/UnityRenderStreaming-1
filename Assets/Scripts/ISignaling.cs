using Unity.WebRTC;

namespace Unity.RenderStreaming
{
    public delegate void OnOfferEventHandler(OfferResData offerResData);
    public delegate void OnIceCandidateEventHandler(CandidateResData candidateResData);

    public interface ISignaling
    {
        void Start();
        void Stop();
        void SendCandidate(string connectionId, RTCIceCandidate candidate);
        void SendAnswer(string connectionId, RTCPeerConnection peerConnection);
        event OnOfferEventHandler OnOffer;
        event OnIceCandidateEventHandler OnIceCandidate;
    }
}
