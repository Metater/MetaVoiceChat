# MetaVoiceChat
 
## A simple and self-contained proximity voice chat solution for Unity's popular client-server networking libraries (currently only is Mirror supported*) with all of the features you need.
&ast; Other libraries can easily be implemented by composing an agnostic MonoBehaviour -- please help with additional implementations.

### A massive thank you to [Vatsal Ambastha](https://github.com/adrenak) and his projects [UniVoice](https://github.com/adrenak/univoice) and [UniMic](https://github.com/adrenak/unimic) that were heavily referenced when starting this project in late 2023.

## Features
- Voice activation detection and latching
- Pooled data buffers with no runtime allocation
- "ECHO" compilation symbol to playback your own voice
- Events for speaking, deafening, input muting, and output muting ([R3](https://github.com/Cysharp/R3) implementation)
- UI settings and indicators with hooks and an official implementation
- Dynamic desync compensation

### Planned Features
- Opus audio encoding
- Documentation

### Missing Features (Compared to [Dissonace Voice Chat](https://assetstore.unity.com/packages/tools/audio/dissonance-voice-chat-70078))
- Audio preprocessing
    - Noise supression
    - Dynamic range compression
    - Automatic gain control
- Audio postprocessing
    - Acoustic echo cancellation
    - Soft clipping
    - Soft channel fade
- Opus features
    - Forward error correction
    - Dynamic bitrate
- Multiple chat rooms
- Tutorials

## Implementations
- [Mirror](https://github.com/MirrorNetworking/Mirror)

### Missing Implementations
- [Netcode for GameObjects](https://docs-multiplayer.unity3d.com/netcode/current/about/)
- [Fish-Net](https://fish-networking.gitbook.io/docs)
- [Dark Rift 2](https://github.com/DarkRiftNetworking/DarkRift)
- [Unity WebRTC](https://github.com/Unity-Technologies/com.unity.webrtc)
- [Photon Unity Networking 2](https://www.photonengine.com/pun)

## Improvements Over [UniVoice](https://github.com/adrenak/univoice)
- Fixed memory leak
- Many playback algorithm improvements
- Lower latency
- Dynamic desync compensation
