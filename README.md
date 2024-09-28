![MetaVoiceChat Banner](MetaVoiceChat.png)

&ast; Currently, only Mirror is supported, however other libraries can easily be implemented by composing an agnostic MonoBehaviour and implementing a minimal interface — please feel free to contribute additional network implementations.

## Table of Contents
- [Thank Yous](#thank-yous)
- [Installation](#installation)
- [Features](#information)
- [Tutorial](#tutorial)

## Installation
1. TODO

## Information
- Simple
    - Default Unity Microphone
    - No code required
    - No complicated cloud services required
- Configurable
    - Settings for...
    - Exposed Opus settings
        - Application: VOIP, Audio, or Restricted Low-Delay
        - Complexity: 0-10
        - Frame size: 10ms, 20ms, or 40ms
        - Signal: Voice, Music, or Auto
    - TODO Jitter
        - Window size in seconds
        - Default value in seconds
    - Output
        - Unity Audio Source
- Functional
    - Functionality and reactive properties with events for
        - Speaking
        - Deafening yourself
        - Input muting yourself
        - Output muting others
    - Voice activation detection and latching
- Modular
    - Abstract VcAudioInput and VcAudioOutput classes
    - Abstract VcInputFilter and VcOutputFilter pipelines
- Testable
    - Echo mode to playback your own voice
    - Sine wave override mode
- Details
    - No memory garbage created at runtime using pooled data buffers
    - Fixed 16kHz sampling frequency
    - Single audio channel per microphone
    - 16-bit PCM audio
    - Latency is ~250ms on average (Unity's crappy microphone is to blame for ~200ms)

- UI settings and indicators with hooks and an official implementation that saves to PlayerPrefs
- Dynamic desync compensation using a latency error P-controller with RMS jitter adjustment
- Opus features
    - Variable bitrate encoding
    - Many exposed settings
- TODO FILL IN OTHER FEATURES
- TODO Set default values for everything
- TODO Add tooltips to all settings (Check audio input and output mic and audio sources)

### Planned Features
- Opus audio encoding
- Documentation
- Push to talk
- Tutorial
    - TODO Include configuration of output audio source, 3d sound and drop off to zero after a distance

## Tutorial
1. TODO
2. Output Audio Source Configuration
    - Place the audio source on the player prefab in the mouth area
    - "Output" = the voice chat audio mixer group
    - "Play On Awake" = false
    - "Loop" is set to true internally, so don't worry
    - "Spacial Blend" = 1 for proximity chat
    - "3D Sound Settings"
        - "Doppler Level" is set to zero internally, so don't worry (it must be zero because of how Unity implements this)
        - "Max Distance" = ~50 meters or whatever you think is best
        - Ensure the volume rolloff curve's last datapoint has a volume of zero

## Tips
- Change Project Settings/Audio/DSP Buffer Size from "Best performance" to "Best latency"

## Thank Yous

### A massive thank you to [Vatsal Ambastha](https://github.com/adrenak) and his projects [UniVoice](https://github.com/adrenak/univoice) and [UniMic](https://github.com/adrenak/unimic) that were heavily referenced when starting this project in late 2023.

### Another thank you to [Concentus: Opus for Everyone](https://github.com/lostromb/concentus) for their native C# implementation of Opus that makes it extremely easy to add Opus to projects like this.

## Implementations
- [Mirror](https://github.com/MirrorNetworking/Mirror)

## Direct Improvements Over [UniVoice](https://github.com/adrenak/univoice)
- Fixed memory leak
- Many playback algorithm improvements
- Dynamic audio buffer latency compensation
- Lower latency

## Missing Things

### Missing Implementations
- [Netcode for GameObjects](https://docs-multiplayer.unity3d.com/netcode/current/about/)
- [LiteNetLib](https://github.com/RevenantX/LiteNetLib)
- [LiteEntitySystem](https://github.com/RevenantX/LiteEntitySystem)
- [Fish-Net](https://fish-networking.gitbook.io/docs)
- [Dark Rift 2](https://github.com/DarkRiftNetworking/DarkRift)
- [Unity WebRTC](https://github.com/Unity-Technologies/com.unity.webrtc)
- [Photon Unity Networking 2](https://www.photonengine.com/pun)

### Missing Features
- Example scene
- Configurable sampling rates
- Multithreading for Opus
- Compared to [Dissonace Voice Chat](https://assetstore.unity.com/packages/tools/audio/dissonance-voice-chat-70078)
    - Audio preprocessing
        - Noise supression
        - Dynamic range compression
        - Automatic gain control
    - Audio postprocessing
        - Acoustic echo cancellation
        - Soft clipping
        - Soft channel fade
    - Certain Opus features
        - Forward error correction
    - Multiple chat rooms

## Writing Extensions

### How do I write a network implementation?
- TODO

### How do I write a VcAudioInput?
- Ideas: transmit an audio file or in-game audio
- TODO

### How do I write a VcAudioOutput?
- Ideas: save audio or do speech-to-text
- TODO

### How do I write a VcInputFilter?
- TODO

### How do I write a VcOutputFilter?
- TODO

## Community Questions
- Should code fail without throwing exceptions? Should silent failure be an option? E.g. VcMicAudioInput and VcAudioClip may throw.
- How can vulnerabilities be found and compensated for?

## Made with MetaVoiceChat
- TODO Bomb Bois Steam page URL

## License
This project is licensed under the [MIT License](LICENSE)

## To-Do
- Default UI
- Abstract selection system for configuring voice chat settings for particular clients that the local player wants to configure

# Ideas
- Text chat implementation with UI?
