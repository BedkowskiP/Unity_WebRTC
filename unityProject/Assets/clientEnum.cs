using System;
using UnityEngine;
using Unity.WebRTC;
using WebSocketSharp;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Collections;

public class clientEnum : MonoBehaviour
{
    private List<IEnumerator> threadPumpList = new List<IEnumerator>();
    private WebSocket ws;
    [SerializeField] private string serverURL = "ws://localhost:6969";
    private string username = null;
    private string localUUID = null;
    private RTCPeerConnection localPeer;
    private RTCRtpSender sender;

    private IDictionary<string, clonePeer> clients = new Dictionary<string, clonePeer>();
    private IDictionary<string, RTCPeerConnection> consumers = new Dictionary<string, RTCPeerConnection>();

    private RTCPeerConnection _transport;
    private MediaStream _sendStream;
    private MediaStream _receiveStream;

    [SerializeField]
    private AudioSource _audioSourceInput;
    [SerializeField]
    private AudioSource _audioSourceOutput;
    private AudioStreamTrack track;

    private string _defaulMicrophone;
    private AudioClip m_clipInput;

    #region payload_classes
    [Serializable]
    public class payload_consume
    {
        public string type = "consume";
        public string id;
        public string consumerId;
        public sdp_c sdp;
    }

    [Serializable]
    public class payload_ice_consumer
	{
        public string type = "consumer_ice";
        public string uqid;
        public string consumerId;
	}

    [Serializable]
    public class payload_ice
    {
        public string type = "ice";
        public ice_c ice;
        public string uqid;
    }

    [Serializable]
    public class payload_connect
	{
        public string type = "connect";
        public sdp_c sdp;
        public string uqid;
        public string username;
    }

    [Serializable]
    public class sdp_c
    {
        public string type = "offer";
        public string sdp;
    }

    [Serializable]
    public class ice_c
	{
        public string candidate;
        public string sdpMid;
        public string sdpMLineIndex;
        public string usernameFragment;
	}
    #endregion


    #region JSON_Classes

    public class J_baptist
    {
        public string type { get; set; }
        public string username { get; set; }
    }

    public class J_Witaj
    {
        public string type { get; set; }
        public string id { get; set; }
    }

    public class J_Peers
    {
        public string type { get; set; }
        public _peer[] peers { get; set; }
    }

    public class J_Producer
    {
        public string type
        {
            get; set;
        }
        public string id
        {
            get; set;
        }
        public string username
        {
            get; set;
        }
    }
    public class J_Answer
    {
        public string type
        {
            get; set;
        }
        public sdp_class sdp
        {
            get; set;
        }
    }
    public class J_user_left
    {
        public string type
        {
            get; set;
        }
        public string id
        {
            get; set;
        }
    }
    public class J_Consume
    {

		public string type
        {
            get; set;
        }
        public string username
        {
            get; set;
        }
        public string id
        {
            get; set;
        }
        public string consumerId
        {
            get; set;
        }
        public sdp_class sdp
        {
            get; set;
        }
	}

    public class sdp_class
    {
        public string type
        {
            get; set;
        }
        public string sdp
        {
            get; set;
        }
    }
    #endregion
    public class _peer
    {

        public string? consumerId { get; set; }
        public string id { get; set; }
        public string username { get; set; }
    }

    public class clonePeer
    {
        public string? consumerId { get; set; }
        public string id { get; set; }
        public string username { get; set; }

        public clonePeer(string id, string username)
        {
            this.consumerId = null;
            this.id = id;
            this.username = username;
        }

        public clonePeer(string consumerId, string id, string username)
        {
            this.consumerId = consumerId;
            this.id = id;
            this.username = username;
        }

        public string toString()
        {
            return "{id = " + this.id + ", username = " + this.username + ", consumerId = " + this.consumerId + "}";
        }
    }
    private IEnumerator HandleMessage(string message)
    {
        Debug.Log("HandleMessage: Start...");
        //try
        //{
        if (message.Contains("\"type\":\"witaj\"") == true)
        {
            Debug.Log("HandleMessage: Wiadomosc zawierala, WITAJ!");
            J_Witaj witaj = JsonConvert.DeserializeObject<J_Witaj>(message);
            //Debug.Log("HandleMessage: Recevied message" + message);
            Debug.Log("HandleMessage: Your ID: " + witaj.id);
            localUUID = witaj.id;
            yield return StartCoroutine(Connect());
        }

        if (message.Contains("\"type\":\"baptism\"") == true)
        {
            Debug.Log("HandleMessage: Wiadomosc zawierala, Baptism!");
            J_baptist baptism = JsonConvert.DeserializeObject<J_baptist > (message);
            Debug.Log("HandleMessage: Your Username:" + baptism.username);
            username = baptism.username;
        }

        if (message.Contains("\"type\":\"peers\"") == true)
        {
            Debug.Log("HandleMessage: Wiadomosc zawierala, peers!");
            J_Peers peers_msg = JsonConvert.DeserializeObject<J_Peers>(message);
            //Debug.Log("HandleMessage: Recevied message: " + message);
            yield return StartCoroutine(HandlePeers(peers_msg));
        }

        if (message.Contains("\"type\":\"newProducer\"") == true)
        {
            Debug.Log("HandleMessage: Wiadomosc zawierala, newProducer!");
            J_Producer producer_msg = JsonConvert.DeserializeObject<J_Producer>(message);
            //Debug.Log("HandleMessage: Recevied message " + message);
            yield return StartCoroutine(HandleProducer(producer_msg));
        }

        if (message.Contains("\"type\":\"answer\"") == true)
        {
            Debug.Log("HandleMessage: Wiadomosc zawierala, answer!");
            J_Answer answer_msg = JsonConvert.DeserializeObject<J_Answer>(message);
            //Debug.Log("HandleMessage: Recevied message " + message);
            yield return StartCoroutine(HandleAnswer(answer_msg));
        }

        if (message.Contains("\"type\":\"user_left\"") == true)
        {
            Debug.Log("HandleMessage: Wiadomosc zawierala, user_left!");
            J_user_left user_left_msg = JsonConvert.DeserializeObject<J_user_left>(message);
                //Debug.Log("HandleMessage: Recevied message " + message);
            yield return StartCoroutine(HandleUserLeft(user_left_msg));
        }

        if (message.Contains("\"type\":\"consume\"") == true)
        {
            Debug.Log("HandleMessage: Wiadomosc zawierala, consume!");
            J_Consume consume_msg = JsonConvert.DeserializeObject<J_Consume>(message);
            //Debug.Log("HandleMessage: Recevied message " + message);
            yield return StartCoroutine(HandleConsume(consume_msg));
        }

        yield break;
    }

    private IEnumerator HandlePeers(J_Peers peers_msg)
    {
        Debug.Log("HandlePeers: Starting...");
        //Debug.Log(peers_msg.peers.Length);
        if (peers_msg.peers.Length > 0)
        {
            foreach (_peer peer in peers_msg.peers)
            {
                Debug.Log("HandlePeers: Peer ID: " + peer.id);
                clonePeer newP = new clonePeer(peer.id, peer.username);
                Debug.Log("HandlePeers: New peer: " + newP.toString());
                clients.Add(peer.id, newP);
                yield return StartCoroutine(ConsumeOnce(newP));
            }
        }
        else
        {
            Debug.Log("HandlePeers: You are alone in session...");
        }
    }

    private IEnumerator HandleProducer(J_Producer producer)
    {
        if (localUUID == producer.id) yield break;
        Debug.Log("HandleProducer: New user in session.");
        clonePeer peer = new clonePeer(producer.id, producer.username);
        clients.Add(producer.id, peer);
        Debug.Log("HandleProducer: List of users: ");
        foreach (var c in clients)
        {
            Debug.Log(c.Value.username + " -> " + c.Key);
        }
        yield return StartCoroutine(ConsumeOnce(peer));
    }

    private IEnumerator HandleAnswer(J_Answer answer)
    {
        Debug.Log("HandleAnswer: Starting...");
        RTCSessionDescription desc = new RTCSessionDescription();
        desc.sdp = answer.sdp.sdp;
        yield return localPeer.SetRemoteDescription(ref desc);
    }

    public IEnumerator HandleUserLeft(J_user_left user_left)
    {
        
        string cId = null;

        try
        {
            cId = clients[user_left.id].consumerId;
            Debug.Log($"HandleUserLeft: User {cId} left the session");
        }
        catch
        {
            Debug.Log("HandleUserLeft: User without an Id");
        }

        try
        {
            var uN = clients[user_left.id].username;
            Debug.Log($"HandleUserLeft: His username: {uN}.");
        }
        catch
        {
            Debug.Log("HandleUserLeft: User without an username");
        }

        if (cId != null)
        {
            yield return StartCoroutine(RemoveConsumer(cId));
        }

        clients.Remove(user_left.id);

        yield break;
    }

    public IEnumerator RemoveConsumer(string consumerId)
	{
        foreach(var transceiver in consumers[consumerId].GetTransceivers())
		{
            if (transceiver.Receiver.Track.Id == consumerId && transceiver.Direction != RTCRtpTransceiverDirection.Stopped)
			{
                transceiver.Stop();
                break;
			}
        }
        foreach (var sender in consumers[consumerId].GetSenders())
        {
            sender.Dispose();
        }

        foreach (var receiver in consumers[consumerId].GetReceivers())
        {
            receiver.Dispose();
        }

        yield break;
	}

    public IEnumerator HandleConsume(J_Consume consume)
    {
        Debug.Log("HandleConsume: Starting...");
        RTCSessionDescription desc = new RTCSessionDescription();
        desc.sdp = consume.sdp.sdp;

        try
        {
            consumers[consume.consumerId].SetRemoteDescription(ref desc);
            Debug.Log("HandleConsume: Remote description set.");
        }
        catch (Exception e)
        {
            Debug.Log("HandleConsume: Error: " + e);
        }

        yield break;
    }

    public IEnumerator ConsumeOnce(clonePeer peer)
    {
        Debug.Log("ConsumeOnce: Starting...");
        Debug.Log("ConsumeOnce: " + peer.id + " " + peer.username);

        yield return StartCoroutine(CreateConsumerTransport(peer));
        RTCPeerConnection transport = _transport;

        if (transport != null)
        {
            sdp_c sdp = new sdp_c();
            sdp.sdp = transport.LocalDescription.sdp;
            payload_consume payload = new payload_consume();
            payload.id = peer.id;
            payload.consumerId = peer.consumerId;
            payload.sdp = sdp;
            string JSON = JsonUtility.ToJson(payload);
            Debug.Log("ConsumeOnce: Sending message: " + JSON);
            ws.Send(JSON);
        } else {
            Debug.Log("ConsumeOnce: Tranport is null.");
        }

        yield return _transport;
    }

    public IEnumerator CreateConsumerTransport(clonePeer peer)
    {
        
        Debug.Log("CreateConsumerTransport: Starting...");
        string consumerID = peer.id;
        RTCPeerConnection consumerTransport = new RTCPeerConnection();
        clients[peer.id].consumerId = consumerID;

        consumers.Add(consumerID, consumerTransport);

        consumers[consumerID].AddTransceiver(TrackKind.Audio).Direction = RTCRtpTransceiverDirection.RecvOnly;

        yield return StartCoroutine(HandleTransportNegotiation(consumers[consumerID]));

        consumers[consumerID].OnIceCandidate += consumerTransport => StartCoroutine(HandleConsumerIceCandidate(consumerTransport, consumerID));
        consumers[consumerID].OnTrack += (RTCTrackEvent e) => StartCoroutine(handleRemoteTrack(e));

        _transport = consumerTransport;
    }
    public IEnumerator handleRemoteTrack(RTCTrackEvent e)
    {
        if (e.Track.Kind == TrackKind.Audio)
        {
            _receiveStream.AddTrack(e.Track);
        }
        yield break;
    }

    public IEnumerator HandleTransportNegotiation(RTCPeerConnection peer)
    {
        Debug.Log("HandleTransportNegotiation: Creating offer...");
        var offer = peer.CreateOffer();
        while(offer.Desc.sdp == null)
            yield return null;

        Debug.Log("HandleTransportNegotiation: Offer created: " + offer.Desc.sdp.ToString());
        yield return StartCoroutine(OnTransportOfferCreateSuccess(offer.Desc, peer));

    }

    public IEnumerator OnTransportOfferCreateSuccess(RTCSessionDescription offer, RTCPeerConnection peer)
    {
        Debug.Log("OnTransportOfferCreateSuccess: Setting consumer description...");
        var desc = peer.SetLocalDescription(ref offer);
        yield return null;
        Debug.Log("OnTransportOfferCreateSuccess: Consumer description set: " + peer.LocalDescription.sdp.ToString());
        yield return desc;
    }

    public IEnumerator Connect()
    {
        Debug.Log("Connect: Starting P2P connection.");
        yield return StartCoroutine(CreatePeer());
        sender = localPeer.AddTrack(track, _sendStream);
        yield return StartCoroutine(Subscribe());
    }
    public IEnumerator CreatePeer()
    {
        Debug.Log("CreatePeer: Creating peer...");
        var config = new RTCConfiguration();
        config.iceServers = new[]{
            new RTCIceServer { urls = new[] { "stun:stun.l.google.com:19302" } },
            new RTCIceServer { urls = new[] { "stun:stun.stunprotocol.org:3478" } }
        };

        localPeer = new RTCPeerConnection(ref config);
        localPeer.OnIceCandidate = (e) => StartCoroutine(HandleIceCandidate(e));
        localPeer.OnNegotiationNeeded += () => StartCoroutine(HandleNegotiation(localPeer));

        yield return localPeer;
    }

    public IEnumerator Subscribe()
    {
        Debug.Log("Subscribe: Starting");
        yield return StartCoroutine(ConsumeAll());
        yield break;
    }
    private IEnumerator ConsumeAll()
	{
        Debug.Log("ConsumeAll: Sending getPeers");
        string getPeers = "{\"type\":\"getPeers\",\"uqid\":\"" + localUUID + "\"}";
        Debug.Log("ConsumeAll: Sending message: " + getPeers);
        ws.Send(getPeers);
        yield break;
	}

    public IEnumerator HandleNegotiation(RTCPeerConnection peer)
    {
        Debug.Log("HandleNegotiation: Creating offer...");
        var offer = peer.CreateOffer();
        yield return offer;
        Debug.Log("HandleNegotiation: Offer created: " + offer.Desc.sdp.ToString());
        yield return StartCoroutine(OnOfferCreateSuccess(offer.Desc, peer));
    }

    public IEnumerator OnOfferCreateSuccess(RTCSessionDescription offer, RTCPeerConnection peer)
    {
        Debug.Log("OnOfferCreateSuccess: Setting local description...");
        var desc = peer.SetLocalDescription(ref offer);
        yield return desc;
        Debug.Log("OnOfferCreateSuccess: Local description set: " + peer.LocalDescription.sdp.ToString());
        sdp_c sdp = new sdp_c();
        sdp.sdp = peer.LocalDescription.sdp.ToString();
        payload_connect payload = new payload_connect();
        payload.sdp = sdp;
        payload.uqid = localUUID;
        payload.username = username;
        string JSON = JsonUtility.ToJson(payload);
        Debug.Log("OnOfferCreateSuccess: Sending message: " + JSON);
        ws.Send(JSON);
    }

    public IEnumerator HandleIceCandidate(RTCIceCandidate candidate)
    {
        if (candidate != null && candidate.Candidate != null && candidate.Candidate.Length > 0)
        {
            ice_c ice = new ice_c();
            ice.candidate = candidate.Candidate;
            ice.sdpMid = candidate.SdpMid;
            ice.sdpMLineIndex = candidate.SdpMLineIndex.ToString();
            ice.usernameFragment = candidate.UserNameFragment;

            payload_ice payload = new payload_ice();
            payload.ice = ice;
            payload.uqid = localUUID;

            string JSON = JsonUtility.ToJson(payload);
            Debug.Log("HandleIceCandidate: Sending message: Sending message: " + JSON);
            ws.Send(JSON);
        }
        yield break;
    }

    private IEnumerator HandleConsumerIceCandidate(RTCIceCandidate candidate, string consumentID)
    {
        if (candidate != null && candidate.Candidate != null && candidate.Candidate.Length > 0)
        {
            payload_ice_consumer ice = new payload_ice_consumer();
            ice.uqid = localUUID;
            ice.consumerId = consumentID;
            string JSON = JsonUtility.ToJson(ice);
            Debug.Log("HandleConsumerIceCandidate: Sending message: " + JSON);
            ws.Send(JSON);
        }
        yield break;
    }

	void Start()
    {
        _defaulMicrophone = Microphone.devices[0];
        Microphone.GetDeviceCaps(_defaulMicrophone, out int minFreq, out int maxFreq);
        m_clipInput = Microphone.Start(_defaulMicrophone, true, 10, 44100);

        _sendStream = new MediaStream();
        _audioSourceInput.clip = m_clipInput;
        _audioSourceInput.Play();

        track = new AudioStreamTrack(_audioSourceInput);

        _receiveStream = new MediaStream();
        _receiveStream.OnAddTrack += OnAddTrack;

        Init();
    }

    private void OnAddTrack(MediaStreamTrackEvent e)
	{
        var track = e.Track as AudioStreamTrack;
        _audioSourceOutput.SetTrack(track);
        _audioSourceOutput.Play();
    }

    private void Init()
    {
        Debug.Log("Init: Starting...");
        ws = new WebSocket(serverURL);

        ws.OnOpen += (sender, e) =>
        {
            Debug.Log("Connection: Connection started.");
            string JSON = "{\"type\":\"user_connected_via_ws\",\"user_type\":\"UNI\"}";
            ws.Send(JSON);
        };

        ws.OnMessage += (sender, e) =>
        {
            Debug.Log("Connection: Recevied message: "+ e.Data);
            threadPumpList.Add(HandleMessage(e.Data));
            Debug.Log("Connection: Message handled.");
        };

        ws.OnClose += (sender, e) =>
        {
            Debug.Log("Connection: Connection closed.");
            ws = null;
        };

        ws.Connect();
    }

	private void Update()
	{
        while(threadPumpList.Count > 0)
		{
            StartCoroutine(threadPumpList[0]);
            threadPumpList.RemoveAt(0);
		} 

	}

}