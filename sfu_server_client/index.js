
let localStream = null;
let my_vid = null;
let localPeer = null;
let connection = null;
let isOpen = false;
let ws_connection_ip = "ws://localhost:6969";
let localUUID = null;
let usernname = null;

let clients = new Map();
let consumers = new Map();

const configuration = {
    iceServers: [
        { 'urls': 'stun:stun.stunprotocol.org:3478' },
        { 'urls': 'stun:stun.l.google.com:19302' },
    ]
};


window.onload = async () => {
    await init();
};


const init = async () => {

    connection = new WebSocket(ws_connection_ip);
    connection.onopen = async () => {
        console.log("Otwarto połączenie")
        isOpen = true;
    }
    connection.onmessage = (msg) => {
        // console.log(msg.type)
        // console.log(msg)
        console.log("Otrzymano wiadomość : ")
        handleMessage(msg);
    }
    connection.onclose = () => {
        console.log("Zamknięto połączenie")
        connection = null;
    }

    username = uuidv4short();
    console.log("UserName : " + username)

}

const connect = async () => {
    //let RoomID = document.getElementById("roomID").value; 
    //if(RoomID == ""){
    //    alert('Podaj ID pokoju...')
    //    return
    //}
    //console.log("ROOM_ID: "+RoomID)
    let stream = await navigator.mediaDevices.getUserMedia({ video: false, audio: true })
    //handleRemoteTrack(stream,username);
    localStream = stream

    //my_vid = document.getElementById("my_video");
    //my_vid.srcObject = localStream

    localPeer = await createPeer();
    localStream.getTracks().forEach(track => localPeer.addTrack(track, localStream));
    await subscribe();
}

const handleRemoteTrack = async (stream, peerid) => {
    //console.log("handleRemoteTrack not implemented")
    let r_vids = document.getElementById("remote_vids");

    // let video = document.createElement('video');
    // video.id= `zdalny_${peerid}`
    // video.srcObject = stream;
    // video.autoplay = true;
    // video.muted = true;

    let audio = document.createElement('audio');
    audio.id = `zdalny_${peerid}`
    audio.srcObject = stream;
    audio.autoplay = true

    //r_vids.append(video)

}

const handleMessage = ({ data }) => {
    const message = JSON.parse(data);
    console.log(message)
    console.log(message.type)
    switch (message.type) {
        case 'witaj':
            localUUID = message.id;
            console.log(localUUID)
            break;
        case 'answer':
            handleAnswer(message);
            break;
        case 'peers':
            handlePeers(message);
            break;
        case 'consume':
            handleConsume(message)
            break
        case 'newProducer':
            handleNewProducer(message);
            break;
        case 'user_left':
            removeUser(message);
            break;
        case 'test':
            handleTest(message)
            break;
    }
}

const handleAnswer = ({ sdp }) => {
    console.log("otrzymano odpoweidź")
    const desc = new RTCSessionDescription(sdp);
    localPeer.setRemoteDescription(desc).catch(e => console.log(e))
}

const uuidv4 = () => {
    return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, function (c) {
        var r = Math.random() * 16 | 0, v = c == 'x' ? r : (r & 0x3 | 0x8);
        return v.toString(16);
    });
}


const uuidv4short = () => {
    return 'U-xxxxxxx'.replace(/[xy]/g, function (c) {
        var r = Math.random() * 16 | 0, v = c == 'x' ? r : (r & 0x3 | 0x8);
        return v.toString(16);
    });
}


const createConsumeTransport = async (peer) => {
    let consumerId = uuidv4();
    let consumerTransport = new RTCPeerConnection(configuration);
    // console.log("clients bef")
    // console.log(clients)
    clients.get(peer.id).consumerId = consumerId;
    // console.log("clients aft")
    // console.log(clients)
    consumerTransport.id = consumerId;
    consumerTransport.peer = peer;
    console.log("conusmer transoprt poniżej...")
    console.log(consumerTransport);
    consumers.set(consumerId, consumerTransport);
    consumers.get(consumerId).addTransceiver('video', { direction: "recvonly" })
    consumers.get(consumerId).addTransceiver('audio', { direction: "recvonly" })
    let offer = await consumers.get(consumerId).createOffer();
    await consumers.get(consumerId).setLocalDescription(offer);

    consumers.get(consumerId).onicecandidate = (event) => handleConsumerIceCandidate(event, peer.id, consumerId);

    consumers.get(consumerId).ontrack = (e) => {
        handleRemoteTrack(e.streams[0], peer.id)
    }

    return consumerTransport
}

const consumeOnce = async (peer) => {
    let transport = await createConsumeTransport(peer);
    let payload = {
        type: 'consume',
        id: peer.id,
        consumerId: transport.id,
        sdp: await transport.localDescription
    }
    connection.send(JSON.stringify(payload))
    console.log(" CONSUME: " + JSON.stringify(payload))
}

const handlePeers = async ({ peers }) => {
    console.log("Odebrano waiadomość: PEERS")
    console.log(peers)
    if (peers.length > 0) {
        for (let peer in peers) {
            clients.set(peers[peer].id, peers[peer]);
            await consumeOnce(peers[peer])
        }
    }
    else {
        console.log("Jesteś sam w sesji...")
    }
}

const handleConsume = ({ sdp, id, consumerId }) => {
    const desc = new RTCSessionDescription(sdp);
    consumers.get(consumerId).setRemoteDescription(desc).catch(e => console.log(e));
}

const handleNewProducer = async ({ id, username }) => {
    if (id === localUUID) return;
    console.log("Nowy użytkownik w sieci!")
    clients.set(id, { id, username });
    console.log(clients)
    await consumeOnce({ id, username });
}

const removeUser = ({ id }) => {
    console.log(`Użytkownik ${id} opuszcza sieć`)

    let cId = null
    let uN = null

    try {
        cId = { consumerId } = clients.get(id);
    } catch (err) {
        console.log("użytkownik bez id... klienta...")
    }

    try {
        uN = { username } = clients.get(id)
    }
    catch (err) {
        console.log("użytkownik bez nazwy... klienta...")
    }
    if (uN) {
        console.log("Jego username to : " + username)
    }
    if (cId) {
        consumers.delete(consumerId);
    }

    clients.delete(id);
    //let out_user = document.getElementById("zdalny_"+id)
    //out_user.remove();
}

const handleTest = (message) => {
    console.log("Test zaliczony")
}



const createPeer = async () => {
    localPeer = await new RTCPeerConnection(configuration);
    localPeer.onicecandidate = (event) => handleIceCandidate(event);
    localPeer.onnegotiationneeded = () => handleNegotiation(localPeer);
    return localPeer
}

const handleIceCandidate = async ({ candidate }) => {
    if (candidate && candidate.candidate && candidate.candidate.length > 0) {
        const payload = {
            type: 'ice',
            ice: candidate,
            uqid: localUUID
        }
        connection.send(JSON.stringify(payload));
    }
}

const handleConsumerIceCandidate = async (e, id, consumerId) => {
    let { candidate } = e;
    if (candidate && candidate.candidate && candidate.candidate.length > 0) {
        const payload = {
            type: 'consumer_ice',
            ice: candidate,
            uqid: id,
            consumerId
        }
        connection.send(JSON.stringify(payload));
    }
}

const handleNegotiation = async (peer, type) => {
    console.log('*** NEGOCJACJE ***')
    const offer = await localPeer.createOffer();
    if (offer) console.log("utworzono oferte")
    await localPeer.setLocalDescription(offer)
    connection.send(JSON.stringify({ type: 'connect', sdp: localPeer.localDescription, uqid: localUUID, username: username }))
}

const subscribe = async () => {
    await consumeAll();
}

const consumeAll = async () => {
    console.log("Wysyłem getPeers")
    const payload = {
        type: 'getPeers',
        uqid: localUUID
    }
    connection.send(JSON.stringify(payload))
}