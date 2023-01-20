const webrtc = require("wrtc");
const { v4: uuidv4 } = require('uuid');
const WebSocket = require('ws');
const express = require('express');

const http = require('http');
const https = require('https');
const fs = require('fs');


const app = express();

app.use(express.static('public'));
let roomFromURL = null
app.get("/:room", (req,res)=>{
    res.send(`${req.params.room}`)
    roomFromURL=req.params.room;
    //console.log(roomFromURL)
})

let serverOptions = {
    listenPort: 6969,
    useHttps: false,
    httpsCertFile: './ssl/client-1.local.crt',
    httpsKeyFile: './ssl/client-1.local.key',
}

let sslOptions = {};


if (serverOptions.useHttps) {
    sslOptions.key = fs.readFileSync(serverOptions.httpsKeyFile).toString();
    sslOptions.cert = fs.readFileSync(serverOptions.httpsCertFile).toString();
}


let webServer = null


if(serverOptions.useHttps) {
    console.log("Utworzono serwer https")
    
    webServer = https.createServer(sslOptions, app);
    webServer.listen(serverOptions.listenPort);
} else {
    console.log("Utworzono serwer http")
    webServer = http.createServer(app);
    webServer.listen(serverOptions.listenPort);
}


const WebSocketServer = WebSocket.Server;


// webServer = http.createServer(app);
// webServer.listen(6969);

let rooms = {}
let peers = new Map();
let consumers = new Map();

const wss = new WebSocketServer({server: webServer});

wss.on('connection', (ws) =>{
    let peerId = uuidv4();
    ws.id = peerId;
    console.log("<-- NEW USER -->")
    ws.on('close', (event) => {
       peers.delete(ws.id);
       consumers.delete(ws.id)


       console.log("User left... " + ws.id)
       console.log("users in the session:")

       function logUsers(value,key,map){
        console.log(`[${key}] = ${value}`)
        }
        peers.forEach(logUsers);

       wss.broadcast(JSON.stringify({
        type:'user_left',
        id: ws.id
       }));
    });

    ws.send(JSON.stringify({ 'type': 'witaj', id: peerId }));
    
    ws.send(JSON.stringify({ 'type': 'test'}));

    ws.onmessage = async (msg) =>{
        let message = null
        try{
            message = JSON.parse(msg.data)
            
            switch(message.type){

                case 'user_connected_via_ws':
                    console.log('New user on ws connection, user_type: '+message.user_type)
                    let tempUsername = usernameGenerator(message.user_type);

                    const payloadWithUsername = {
                        type: 'baptism',
                        username: tempUsername,
                    }
                    console.log("Wysyłam username")
                    ws.send(JSON.stringify(payloadWithUsername))
                    break;
                case 'connect':
                    console.log(message)
                    peers.set(message.uqid, {socket:ws});
                    let peer = createPeer();
                    peers.get(message.uqid).username = message.username;
                    peers.get(message.uqid).peer = peer;
                    peer.ontrack = (event) =>{ handleTrackEvent(event,message.uqid,ws)};
                    let desc = new webrtc.RTCSessionDescription(message.sdp);
                    await peer.setRemoteDescription(desc);
                    let answer = await peer.createAnswer();
                    await peer.setLocalDescription(answer);

                    const payload = {
                        type: 'answer',
                        sdp: peer.localDescription
                    }
                    console.log("Wysyłam answer")
                    ws.send(JSON.stringify(payload));
                    break;

                case 'getPeers':
                    console.log(message);
                    let uuid = message.uqid;

                    let list = []

                    peers.forEach((peer,key) =>{
                        if(key!=uuid){
                            const peerInfo = {
                                id:key,
                                username: peer.username,
                            }
                            list.push(peerInfo);
                        }
                    });

                    const peersPayload = {
                        type: 'peers',
                        peers: list
                    }
                    //console.log(peersPayload)
                    ws.send(JSON.stringify(peersPayload));
                    break;
                
                case 'consume':
                    try{
                        console.log(message);
                        let {id, sdp, consumerId} = message;
                        let remoteUser = peers.get(id);
                        ////console.log(remoteUser.stream)
                        let newPeer = await createPeer();
                        ////console.log(remoteUser)
                        consumers.set(consumerId, newPeer);
                        let _desc = new webrtc.RTCSessionDescription(sdp);
                        await consumers.get(consumerId).setRemoteDescription(_desc);

                        remoteUser.stream.getTracks().forEach(track =>{
                            consumers.get(consumerId).addTrack(track, remoteUser.stream);
                        });

                        let _answer = await consumers.get(consumerId).createAnswer();
                        await consumers.get(consumerId).setLocalDescription(_answer);

                        let _payload = {
                            type : 'consume',
                            sdp:consumers.get(consumerId).localDescription,
                            username: remoteUser.username,
                            id,
                            consumerId
                        }
                        ws.send(JSON.stringify(_payload));
                    }catch(error){
                        //console.log(error)
                    }
                    break;
                    case 'ice':
                        console.log(message);
                        console.log("ICE")
                        console.log(JSON.stringify(message))
                        let user = peers.get(message.uqid);
                        if(user.peer){
                            user.peer.addIceCandidate(new webrtc.RTCIceCandidate(message.ice).catch(e => console.log(e)));
                        }
                        break;

                    case 'consumer_ice':
                        console.log("consumer_ice!")
                        console.log(message);
                        let user2 = peers.get(message.uqid);
                        if(user2.peer){
                            user2.peer.addIceCandidate(new webrtc.RTCIceCandidate(message.ice).catch(e => console.log(e)));
                        }
                       
                    
                        break;
                    case 'create':
                        break;
                    case 'join':
                        break;
                    case "leave":
                        break;
                    
            }

        }catch(e){
            message = msg.data
            if(msg.data.type != undefined){
                console.log(msg.data)
            }
            
        }
        
    }

})

wss.broadcast = (data) => {
    peers.forEach((peer) => {
        if (peer.socket.readyState === WebSocket.OPEN) {
            peer.socket.send(data);
        }
    });
};


const handleTrackEvent = async (e,peer,ws) => {
    ////console.log("Czy coś tu nie działa?")
    ////console.log(e)
    ////console.log(e.stream)
    ////console.log(e.streams[0])
    if(e.streams && e.streams[0]){
        
        peers.get(peer).stream = e.streams[0];

        let payload = {
            type: 'newProducer',
            id: peer,
            username: peers.get(peer).username
        }

        wss.broadcast(JSON.stringify(payload))
    }
}

const createPeer = () =>{
    let peer = new webrtc.RTCPeerConnection({
        iceServers: [{ urls: ["stun:stun1.l.google.com:19302", "stun:stun2.l.google.com:19305" ] }]
    });

    return peer;
}


const generalinformation = (ws) =>{
    let obj;
    if(ws["room"] === undefined)
    obj = {
        "type":"info",
        "params":{
            "room":ws["room"],
            "no-clients":room[ws["room"]].lenght,
        }
    }
    else{
        obj = {
            "type":"info",
            "params":{
                "room":"no room"
            }
        }
    }
    ws.send(JSON.stringify(obj));
}

const usernameGenerator = (user_type) =>{

    switch(user_type){
        case 'B':
            return 'U-xxxxxxx'.replace(/[xy]/g, function (c) {
                var r = Math.random() * 16 | 0, v = c == 'x' ? r : (r & 0x3 | 0x8);
                return v.toString(16);
            });
            break;
        case 'UNI':
            return 'UNI-xxxxxxx'.replace(/[xy]/g, function (c) {
                var r = Math.random() * 16 | 0, v = c == 'x' ? r : (r & 0x3 | 0x8);
                return v.toString(16);
            });
            break;
    }
    
}

const createRoom = () => {
    const roomID = roomFromURL;
    rooms[roomID] = [ws];
    ws["room"] = roomID;

    generalinformation()
}

const joinRoom = () => {

}

const leaveRoom = () =>{

}


