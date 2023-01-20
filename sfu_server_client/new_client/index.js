

let localStream = null;
let my_vid = null;
let localPeer = null;
let connection = null;
let isOpen = false;
let ws_connection_ip = "ws://localhost:6969";
let localUUID = null;
let MyUsername = null;

let clients = new Map();
let consumers = new Map();

const configuration = {
    iceServers: [{ urls: ["stun:stun1.l.google.com:19302", "stun:stun2.l.google.com:19305" ] }]
};


window.onload = async () => {
    document.getElementById("disconnect_btn").disabled = true;
};


const init = async () =>{

    console.log('%cinit wystartowało ','color:green')
    //username = uuidv4short();
    //console.log("UserName : "+username)

    connection = new WebSocket(ws_connection_ip);
    connection.onopen = async () =>{
        console.log("Otwarto połączenie")
        
        let payload = {
            type: 'user_connected_via_ws',
            user_type: 'B',
            //username: username,
        }
        connection.send(JSON.stringify(payload))

        isOpen = true;
    }
    connection.onmessage = (msg) => {
        handleMessage(msg);
    }
    connection.onclose = () =>{
        console.log("Zamknięto połączenie")
        connection = null;
        MyUsername = null
    }

    
    
}

const disconnect = async () => {
    document.location.reload()
}

const connect = async () =>{
    document.getElementById("connect_btn").disabled = true;
    document.getElementById("disconnect_btn").disabled = false;
    console.log('%cRozpoczynam procedure "connect"','color:green')
    init();

    let stream = await navigator.mediaDevices.getUserMedia({video:false,audio:true})

    localStream = stream

    localPeer = await createPeer();
    localStream.getTracks().forEach(track => localPeer.addTrack(track, localStream));
    subscribe();
}

const handleRemoteTrack = async (stream, peerid) =>{
    let r_vids = document.getElementById("remote_vids");
    let audio = document.createElement('audio');
    audio.id=`zdalny_${peerid}`
    audio.srcObject = stream;
    audio.autoplay = true
    
}

const handleMessage = ({ data }) => {
    try{
        const message = JSON.parse(data);
        console.log("Otrzymano wiadomość JSON od servera typu: \""+message.type+ "\" o zawartości: "+message);
        console.log(message)
        switch (message.type) {
            case 'witaj':
                localUUID = message.id;
                console.log("localUUID zostało ustawione na:" + localUUID)
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
            case 'baptism':
                handleBaptism(message);
                break;
        }
    }catch{
        console.log(message)
    }
    
}

const handleAnswer = ({sdp}) =>{
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


const createConsumeTransport = async (peer) =>{
    let consumerId = peer.id;
    let consumerTransport = new RTCPeerConnection(configuration);
    clients.get(peer.id).consumerId = consumerId;
    consumerTransport.id = consumerId;
    consumerTransport.peer = peer;
    consumers.set(consumerId,consumerTransport);
    consumers.get(consumerId).addTransceiver('video', { direction: "recvonly" })
    consumers.get(consumerId).addTransceiver('audio', { direction: "recvonly" })
    
    console.log("conusmers -> ")
    function logConsumers(value,key,map){
        console.log(`[${key}] = ${value}`)
    }
    consumers.forEach(logConsumers);

    let offer = await consumers.get(consumerId).createOffer();
    await consumers.get(consumerId).setLocalDescription(offer);

    consumers.get(consumerId).onicecandidate = (event) => handleConsumerIceCandidate(event, peer.id, consumerId);

    consumers.get(consumerId).ontrack = (e) =>{
        handleRemoteTrack(e.streams[0],peer.id)
    }

    return consumerTransport
}

const consumeOnce = async (peer) => {
    let transport = await createConsumeTransport(peer);
    console.log("Wysyłam CONSUME")
    let payload = {
        type: 'consume',
        id: peer.id,
        consumerId: transport.id,
        sdp: await transport.localDescription
    }
    connection.send(JSON.stringify(payload))
    //console.log(" CONSUME: " + JSON.stringify(payload))
}

const handlePeers = async ({peers}) =>{
    console.log('%cHandlePeers START','color:green')
    console.log("Odebrano waiadomość: PEERS")
    console.log(peers)
    if(peers.length > 0){
        console.log("Uzytkownicy w sieci: "+peers.length)
        for(let peer in peers){
            clients.set(peers[peer].id,peers[peer]);
            await consumeOnce(peers[peer])
        }
    }
    else{
        console.log("Obecnie jest 0 użytkowników w sesji... Jesteś tu sam...")
    }
}

const handleConsume = ({sdp,id,consumerId}) =>{
    const desc = new RTCSessionDescription(sdp);
    consumers.get(consumerId).setRemoteDescription(desc).catch(e => console.log(e+" "+consumerId));
}

const handleNewProducer = async ({id,username}) =>{
    if(id=== localUUID) return;
    console.log("Nowy użytkownik w sieci!")
    clients.set(id,{id,username});
    console.log(clients)
    await consumeOnce({id,username});
}

const removeUser = ({id}) =>{
    console.log(`Użytkownik ${id} opuszcza sieć`)
  
    let cId = null
    let uN = null
  
    try{
        cId = {consumerId} = clients.get(id);
    }catch(err){
        console.log("użytkownik bez id... klienta...")
    }
    
    try{
        uN = {username} = clients.get(id)
    }
    catch (err){
        console.log("użytkownik bez nazwy... klienta...")
    }
    if(uN){
        console.log("Jego username to : "+ username) 
    }
    if(cId){
        consumers.delete(consumerId);
    }
    
    clients.delete(id);
    //let out_user = document.getElementById("zdalny_"+id)
    //out_user.remove();
}

const handleTest = (message) =>{
    console.log('%cTest Zaliczony! ','color:green')
}

const handleBaptism = ({username}) =>{
    MyUsername = username;
    console.log("MyUsername = "+MyUsername);
}



const createPeer = async () =>{
    localPeer = await new RTCPeerConnection(configuration);
    localPeer.onicecandidate = (event) => handleIceCandidate(event);
    localPeer.onnegotiationneeded = () => handleNegotiation(localPeer);
    return localPeer
}



const handleIceCandidate = async ({ candidate }) =>{
    if (candidate && candidate.candidate && candidate.candidate.length > 0) {
        const payload = {
            type: 'ice',
            ice: candidate,
            uqid: localUUID
        }
        connection.send(JSON.stringify(payload));
    }
}

const handleConsumerIceCandidate = async (e,id,consumerId) =>{
    let { candidate } = e;
    console.log("Wysyłam consummer ICE")
    if( candidate && candidate.candidate && candidate.candidate.length > 0){
        const payload = {
            type: 'consumer_ice',
            ice: candidate,
            uqid: id,
            consumerId
        }
        connection.send(JSON.stringify(payload));
    }
}

const handleNegotiation = async (peer, type) =>{
    console.log('%c*** NEGOCJACJE ***','color:red')
    const offer = await localPeer.createOffer();
    if(offer) console.log("-Utworzono oferte-")
    await localPeer.setLocalDescription(offer)
    connection.send(JSON.stringify({type:'connect', sdp: localPeer.localDescription, uqid: localUUID, username:MyUsername}))
}

const subscribe = async () => {
    console.log('%c"subscribe" started...','color:green')
    await consumeAll();
}

const consumeAll = async () =>{
    console.log('%c"consumeAll" started... Wysyłam wiadomość getPeers','color:green')
    const payload = {
        type: 'getPeers',
        uqid: localUUID
    }

    connection.send(JSON.stringify(payload))
}