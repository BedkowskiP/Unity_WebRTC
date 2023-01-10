using System;
using UnityEngine;
using Unity.WebRTC;
using WebSocketSharp;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Collections;
using System.Threading.Tasks;
using UnityEditor.VersionControl;
using Task = System.Threading.Tasks.Task;
using System.Security.Cryptography;

public class clientEnum : MonoBehaviour
{
    private List<IEnumerator> threadPumpList = new List<IEnumerator>();
    private WebSocket ws;
    [SerializeField] private string serverURL = "ws://localhost:6969";
    private string username = null;
    private string localUUID = null;
    private RTCPeerConnection localPeer = null;
    private MediaStream localStream = null;
    private MediaStream remoteStream = null;

    private IDictionary<string, clonePeer> clients = new Dictionary<string, clonePeer>();
    private IDictionary<string, RTCPeerConnection> consumers = new Dictionary<string, RTCPeerConnection>();

    // ---
    private List<RTCRtpSender> pc1Senders;

    private DelegateOnIceConnectionChange pc1OnIceConnectionChange;
    private DelegateOnIceCandidate OnIceCandidate;
    private DelegateOnNegotiationNeeded pc1OnNegotiationNeeded;


    // ---

    private RTCPeerConnection _transport;

    public class payload_consume
    {
        public string type = "consume";
        public string id;
        public string consumerId;
        public sdp_class sdp = new sdp_class();

        public payload_consume(string type, string id, string consumerId, string sdp_type, string sdp)
        {
            this.type = type;
            this.id = id;
            this.consumerId = consumerId;
            this.sdp.type = sdp_type;
            this.sdp.sdp = sdp;
        }

        public string toString()
        {
            return "{\"type\":\"consume\",\"id\":\"" + this.id + "\",\"consumerId\":\"" + this.consumerId + "\",\"sdp\":\"" + sdp + "\"}";
        }
    }

    #region JSON_Classes
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

    public class RTCPeerConnection_1
    {
        public string id { get; set; }
        public clonePeer peer { get; set; }
        public RTCPeerConnection peerConnection { get; set; }

        public RTCPeerConnection_1(string localUUID, clonePeer peer, RTCPeerConnection consumerTransport)
        {
            this.id = localUUID;
            this.peer = peer;
            this.peerConnection = consumerTransport;
        }
    }

    private string UuidUnity()
    {
        const string chars = "abcdefghijklmnopqrstuvwxyz1234567890";
        string uid = "UNI-";
        for (int i = 0; i < 7; i++)
        {
            System.Random rand = new System.Random();
            uid += chars[rand.Next(0, chars.Length)];
        }
        return uid;
    }


    private IEnumerator HandleMessage(string message)
    {
        Debug.Log("HandleMessage: Start...");
        //try
        //{
        if (message.Contains("\"type\":\"witaj\"") == true)
            {
                Debug.Log("HandleMessage: Wiadomo�� zawiera�a, WITAJ!");

                J_Witaj witaj = JsonConvert.DeserializeObject<J_Witaj>(message);
                Debug.Log("HandleMessage: Recevied message" + message);
                Debug.Log("HandleMessage: Your ID: " + witaj.id);
                localUUID = witaj.id;
                yield return StartCoroutine(Connect());
            }

            if (message.Contains("\"type\":\"peers\"") == true)
            {
                Debug.Log("HandleMessage: Wiadomo�� zawiera�a, peers!");
                J_Peers peers_msg = JsonConvert.DeserializeObject<J_Peers>(message);
                Debug.Log("HandleMessage: Recevied message: " + message);
                //Debug.Log(peers_msg.peers[0].username);
                yield return StartCoroutine(HandlePeers(peers_msg));
            }

            if (message.Contains("\"type\":\"newProducer\"") == true)
            {
                Debug.Log("HandleMessage: Wiadomo�� zawiera�a, newProducer!");
                J_Producer producer_msg = JsonConvert.DeserializeObject<J_Producer>(message);
                Debug.Log("HandleMessage: Recevied message " + producer_msg);
                yield return StartCoroutine(HandleProducer(producer_msg));
            }

            if (message.Contains("\"type\":\"answer\"") == true)
            {

                Debug.Log("HandleMessage: Wiadomo�� zawiera�a, answer!");
                J_Answer answer_msg = JsonConvert.DeserializeObject<J_Answer>(message);
                Debug.Log("HandleMessage: Recevied message " + answer_msg);
                yield return StartCoroutine(HandleAnswer(answer_msg));
            }

            if (message.Contains("\"type\":\"user_left\"") == true)
            {
                Debug.Log("HandleMessage: Wiadomo�� zawiera�a, user_left!");
                J_user_left user_left_msg = JsonConvert.DeserializeObject<J_user_left>(message);
                Debug.Log("HandleMessage: Recevied message " + user_left_msg);
                yield return StartCoroutine(HandleUserLeft(user_left_msg));
            }

            if (message.Contains("\"type\":\"consume\"") == true)
            {
                Debug.Log("HandleMessage: Wiadomo�� zawiera�a, consume!");
                J_Consume consume_msg = JsonConvert.DeserializeObject<J_Consume>(message);
                Debug.Log("HandleMessage: Recevied message " + consume_msg);
                yield return StartCoroutine(HandleConsume(consume_msg));
            }
            
        try
        {
            if (message.Contains("\"type\":\"test\"") == true)
            {
                Debug.Log("HandleMessage: Wiadomo�� zawiera�a, test!");
            }
        }
        catch (Exception e)
        {
            Debug.Log(e);
        }
        yield return null;
        //await Task.Yield();
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
                yield return ConsumeOnce(newP);
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
        Debug.Log("HandleProducer: List of users: " + clients);
        yield return StartCoroutine(ConsumeOnce(peer));
    }

    private IEnumerator HandleAnswer(J_Answer answer)
    {
        Debug.Log("HandleAnswer: Starting...");
        RTCSessionDescription desc = new RTCSessionDescription();
        desc.sdp = answer.sdp.sdp;
        localPeer.SetRemoteDescription(ref desc);
        yield return null;
    }

    public IEnumerator HandleUserLeft(J_user_left user_left)
    {
        Debug.Log($"HandleUserLeft: User {user_left.id} left the session");
        string cId = null;

        try
        {
            cId = clients[user_left.id].consumerId;
        }
        catch
        {
            Debug.Log("HandleUserLeft: User without an Id");
        }

        try
        {
            var uN = clients[user_left.id].username;
        }
        catch
        {
            Debug.Log("HandleUserLeft: User without an username");
        }

        if (cId != null)
        {
            clients.Remove(cId);
        }

        yield return null;
    }

    public IEnumerator HandleConsume(J_Consume consume)
    {
        Debug.Log("HandleConsume: Starting...");
        RTCSessionDescription desc = new RTCSessionDescription();
        desc.sdp = consume.sdp.sdp;

        try
        {
            consumers[consume.consumerId].SetRemoteDescription(ref desc);
        }
        catch (Exception e)
        {
            Debug.Log("HandleConsume: Error: " + e);
        }

        yield return null;
    }

    public IEnumerator ConsumeOnce(clonePeer peer)
    {
        Debug.Log("ConsumeOnce: Starting...");
        Debug.Log("ConsumeOnce: " + peer.id + " " + peer.username);

        yield return StartCoroutine(CreateConsumerTransport(peer));
        RTCPeerConnection transport = _transport;

        if (transport != null)
        {
            //Debug.Log("ConsumeOnce: Tranport not null.");
            //payload_consume payload = null;
            //string temp = null;

            //try
            //{
            //    payload = new payload_consume("consume", peer.id, peer.consumerId, "offer", transport.LocalDescription.sdp);
            //    temp = JsonUtility.ToJson(transport.LocalDescription.sdp);
            //    Debug.Log("ConsumeOnce: " + temp);
            //}
            //catch (Exception e)
            //{
            //    Debug.Log("ConsumeOnce: Error: " + e);
            //}

            //Debug.Log(temp);
            string JSON = "{type:\'consume\', id:\'" + peer.id + "\', consumerId:\'" + peer.consumerId + "\', sdp:{type:\'offer\',sdp:\'" + transport.LocalDescription.sdp + "\'}}";
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
        string consumerID = UuidUnity();
        RTCPeerConnection consumerTransport = new RTCPeerConnection();
        clients[peer.id].consumerId = consumerID;
        //Debug.Log("CreateConsumerTransport: " + clients[peer.id].consumerId);
        consumers.Add(consumerID, consumerTransport);

        //consumers[consumerID].AddTransceiver();

        yield return StartCoroutine(HandleTransportNegotiation(consumers[consumerID]));

        consumers[consumerID].OnIceCandidate += consumerTransport => HandleConsumerIceCandidate(consumerTransport, peer.id, consumerID);

        yield return null;
        _transport = consumerTransport;
    }

    public IEnumerator HandleTransportNegotiation(RTCPeerConnection peer)
    {
        Debug.Log("HandleTransportNegotiation: Creating offer...");
        var offer = peer.CreateOffer();
        yield return null;

        Debug.Log("HandleTransportNegotiation: Offer created: " + offer.Desc.sdp.ToString());
        yield return StartCoroutine(OnTransportOfferCreateSuccess(offer.Desc, peer));

    }

    public IEnumerator OnTransportOfferCreateSuccess(RTCSessionDescription offer, RTCPeerConnection peer)
    {
        Debug.Log("OnTransportOfferCreateSuccess: Setting consumer description...");
        //Debug.Log("OnTransportOfferCreateSuccess: Offer: " + offer.sdp.ToString());
        var desc = peer.SetLocalDescription(ref offer);
        yield return null;
        Debug.Log("OnTransportOfferCreateSuccess: Local description set: " + peer.LocalDescription.sdp.ToString());
        yield return desc;
    }

    public IEnumerator Connect()
    {
        Debug.Log("Connect: Starting P2P connection.");
        yield return StartCoroutine(CreatePeer(localPeer));
        StartCoroutine(Subscribe());
    }

    public IEnumerator Subscribe()
    {
        Debug.Log("Subscribe: Starting...");
        StartCoroutine(ConsumeAll());
        yield return null;
    }

    public IEnumerator ConsumeAll()
    {
        Debug.Log("ConsumeAll: Sending getPeers");
        string getPeers = "{\"type\":\"getPeers\",\"uqid\":\"" + localUUID + "\"}";
        Debug.Log("ConsumeAll: Sending message: " + getPeers);
        ws.Send(getPeers);
        yield return null;
    }

    public IEnumerator CreatePeer(RTCPeerConnection localPeer)
    {
        Debug.Log("CreatePeer: Creating peer...");
        localPeer = new RTCPeerConnection();
        localPeer.OnIceCandidate = e => HandleIceCandidate(e);
        localPeer.OnNegotiationNeeded += () => StartCoroutine(HandleNegotiation(localPeer));
        yield return localPeer;
    }

    public IEnumerator HandleNegotiation(RTCPeerConnection peer)
    {
        Debug.Log("HandleNegotiation: Creating offer...");
        var offer = peer.CreateOffer();
        yield return offer;
        if (offer != null) Debug.Log("HandleNegotiation: Offer created!");
        OnOfferCreateSuccess(offer.Desc, peer);
        yield return null;
    }

    public IEnumerator OnOfferCreateSuccess(RTCSessionDescription offer, RTCPeerConnection peer)
    {
        Debug.Log("onOfferCreateSuccess: Setting local description...");
        var desc = peer.SetLocalDescription(ref offer);
        yield return desc;
        Debug.Log("OnOfferCreateSuccess: Consumer description has been set.");
        string JSON = "{\"type\":\"connect\",\"sdp\":\"" + offer + "\",\"uqid\":\"" + localUUID + "\",\"username\":\"" + username + "\"}";
        Debug.Log("HandleIceCandidate: Sending message: " + JSON);
        ws.Send(JSON);
        yield return null;
    }

    public void HandleIceCandidate(RTCIceCandidate candidate)
    {
        if (candidate != null && candidate.Candidate != null && candidate.Candidate.Length > 0)
        {
            string JSON = "{\"type\":\"ice\",\"ice\":\"" + candidate + "\",\"uqid\":\"" + localUUID + "\"}";
            Debug.Log("HandleIceCandidate: Sending message: Sending message: " + JSON);
            ws.Send(JSON);
        }
    }

    private void HandleConsumerIceCandidate(RTCIceCandidate candidate, string id, string consumentID)
    {
        if (candidate != null && candidate.Candidate != null && candidate.Candidate.Length > 0)
        {
            string JSON = "{\"consumer_ice\":\"ice\",\"ice\":\"" + candidate + "\",\"uqid\":\"" + localUUID + "\",\"consumerId\":\"" + consumentID + "\"}";
            Debug.Log("HandleIceCandidate: Sending message: " + JSON);
            ws.Send(JSON);
        }
    }

	void Start()
    {
        Init();
    }

    private void Init()
    {
        Debug.Log("Init: Starting...");
        pc1Senders = new List<RTCRtpSender>();

        ws = new WebSocket(serverURL);

        ws.OnOpen += (sender, e) =>
        {
            Debug.Log("Connection: Connection started.");
        };

        ws.OnMessage += async (sender, e) =>
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


