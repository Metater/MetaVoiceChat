# Opus Links
- [Opus Recommended Settings](https://wiki.xiph.org/Opus_Recommended_Settings)
- [libopus 1.1.2](https://opus-codec.org/docs/opus_api-1.1.2/index.html)
- [VisioForge Opus](https://www.visioforge.com/help/docs/dotnet/general/audio-encoders/opus)
- [Concentus: Opus for Everyone](https://github.com/lostromb/concentus)

# RNNoise Links
- [RNNoise4Unity GitHub](https://github.com/adrenak/RNNoise4Unity)
- [rnnoise](https://github.com/xiph/rnnoise)

# WebRTC AEC3/AGC2/NetEQ Links
- [Metater/meta-voice-chat-neteq](https://github.com/Metater/meta-voice-chat-neteq)
- [Metater/meta-voice-chat-aec3](https://github.com/Metater/meta-voice-chat-aec3)
- [How WebRTC’s NetEQ Jitter Buffer Provides Smooth Audio](https://webrtchacks.com/how-webrtcs-neteq-jitter-buffer-provides-smooth-audio/)
- [security-union/videocall-rs/neteq](https://github.com/security-union/videocall-rs/tree/main/neteq)
- [Google NetEQ](https://chromium.googlesource.com/external/webrtc/+/master/modules/audio_coding/neteq/g3doc/index.md)
- [RubyBit/aec3-rs](https://github.com/RubyBit/aec3-rs)
- [Automatic gain control (AGC): keeping voice level consistent](https://www.forasoft.com/learn/audio-for-video/articles-audio/automatic-gain-control-agc2)


# To-Do
- Ensure liscences for mvc-aec3 and mvc-neteq, and Pro are good
- Add MIT liscence to root project, but exclude Pro
- Voice recorder and playback system
- Parrot random voice recorder and playback
- Vosk for Pro
- Switch to a Unity package for RNNoise dependency and other reasons

# [Diagram](<https://asciiflow.com/#/share/eJzVWc1O20AQfpWVLw0SEYLSH7hFIYUeCm0C4ZKLcRZi4ey69hpIEVJl9dgDhyjl0EfgyAnxNHmSbuI4ie3s7qxjItWyFCex55ud79uZ2fWtQcwuNnZJ4DjrhmP2sGfsGrct4wp7vk1Jy9jdWm8ZN%2Fxz590mv%2BqNfvmww68YvmH8S8tAkGPY%2Fz3s%2F0ye96Ano6PVIkCcsEltC%2FPPV7Hej51%2FjC8eCsVZFKf4HLwI%2FgIGEoIenhCb9dAX2%2FKo26EEHkiI9b5wbLN4pk5geCfosvBpnYPnlXAhjS7%2F88C%2B6JS%2Fmr6PPtkOw54uGwr7K%2BBDjK%2FgShhhjTiDvBj9V7Fo4DPbQjWrQxVPaNuvmsTCjmMynlBRqVKrvl0b9v8uLdJUBEC%2BSAiXcC5jPoMHUd7s6cGL6g59%2ByJhVTuU%2BlhLTxroQjnDUsWcRXASe4JrReZ3uP0RXR78QHXsm13XSaf8hGfhIbV9jPZNhlHp2mYd5Hq47FHH4ZoOZSgi6ckSTb4MrJI5UO8JZ4Qjy%2BViRH5s7BSf1Y%2BriVjnFpPEz7BpMt90UKV7ZvqsY77xx3dNOW0EnEp%2F1PXNvAg33y%2BWBsdJ%2BviUcmROO%2FXDMcB21FqgCWru2pNPSrHP4Sn1LjOFdPy15GGLdruYtHF7LR3D%2BW%2BgCaqoX9NgRV4ddzxstjNepUYKFOyD0G%2FeHPuXqOFibHU4qRa94JzEjI896S%2BCy6Z%2BpVDFKk16BJyqkDHLK8MCqMFzJWC0a44K%2F75pk%2BGfX4IoCg9gH1elhPEkyav%2FfnVrTWNxslwft1Av4AULDHupqrc0enjiYy%2FPcgVqf%2FTRpE7QxUh5ew77OVNZIegC8hYzB16sA5HDBj1nqOrYrmuTi4JnhCiwrz8fpg4oG1u4da0cK21y4VRK%2B0XFTzAyUwjqQaoL6v28mcnt9wLjaWuKoGS8DZuVPVRipuviNqLn52iyros6qiMXx11UomdWhiaDo6yPyp7gYWYmWdAXGX%2FMPKvh7dyR%2BCkdfmmGyaWeBLIqZIPnIzfwUY1YtI2TNR868KQPRfUwE%2B%2ByFpfLW%2FK9uUKb2aV9CQ8xu%2BYdOvrq0Su7zWv7hCZggZBbhxAF6bFhoQfsKktnhs7%2BOBAt%2FEwY9ghmevvjQOsS1erskhewmzueRCvXdRLyRfz8Ip3vYU2di9ayE4SVa120ik26pVEJwLjANevUh3Cc%2F%2FUCHqNFFv6XjJ9zBkGnD3QGQTmVdtgj3kglaNs0ehdS51JrWkcBcwNoOgPuWkVoxW4iQncT%2BEjGY8wEQZtKKDOaRMlmeoYGnuVq39KBVVEFndPcko%2BtwLNZrxwQm5KNUSalluk4Zc%2Ff4FUOfwehCdzSn%2BbwDWUdaRSXdRTzuohXbkjZK0xfMkc6b9DA03lfr7SeO0rgvRXjzrj7B9QZ%2FgQ%3D)>)
<div align="center">
<pre style="white-space: pre; overflow-x: auto;">
                                     ┌─────┐                                           
                                     │Voice│                                           
                                     └──┬──┘                                           
                               ┌────────▼───────┐                                      
                               │Unity Microphone│                                      
                               └────────┬───────┘                                      
    ┌───────────────────────►  ┌────────▼───────┐                                      
    │                          │High-Pass Filter│                                      
    │                          └────────┬───────┘                                      
    │                         ┌─────────▼─────────┐                                    
    │                         │   Acoustic Echo   │                                    
    │                         │Cancellation (AEC3)├─────────────────────┐              
    │                         └─────────┬─────────┘                     │              
    │                                   ▼                               │              
    │                        ┌───────Choose───────┐                     │              
    │                ┌───────▼───────┐            │        ┌────────────┴─────────────┐
    │                │48 kHz Resample│            │        │Noise Gate (with pre-roll)│
    │                └───────┬───────┘   ┌────────▼───────┐└────────────┬─────────────┘
    │               ┌────────▼────────┐  │     WebRTC     │     ┌───────▼───────┐      
    │               │Vatsal Ambastha's│  │Noise Supression│     │16 kHz Resample│      
┌───┴──┐            │  RNNoise4Unity  │  └────────┬───────┘     └───────┬───────┘      
│Worker│            │  (recommended)  │           │         ┌───────────▼───────────┐  
│Thread│            └────┬────────────┘           │         │Vosk Speech Recognition│  
└───┬──┘                 │     ┌──────────────┐   │         └───────────────────────┘  
    │                    └─────►Automatic Gain◄───┘                                    
    │                          │Control (AGC2)│                                        
    │                          └───────┬──────┘                                        
    │                          ┌───────▼───────┐                                       
    │                          │User Microphone│                                       
    │                          │    Volume     │                                       
    │                          └───────┬───────┘                                       
    │                           ┌──────▼──────┐                                        
    │                           │Soft Clipping│                                        
    │                           └──────┬──────┘                                        
    │                                  ▼                                               
    │                ┌──────────────Choose─────────┐                                   
    │                │                │            │                                   
    │     ┌──────────▼──────────┐  ┌──▼─┐    ┌─────▼────┐                              
    │     │VAD (tapped off AEC3)│  │Open│    │Noise Gate│                              
    │     └──────────┬──────────┘  └─┬──┘    └─────┬────┘                              
    │                │          ┌────▼──────┐      │                                   
    │                └──────────►Opus Encode◄──────┘                                   
    └─────────────────────────► └────┬──────┘                                          
                         ┌───────────▼───────────┐                                     
                         │Network Provider Encode│                                     
                         └───────────┬───────────┘                                     
                                ┌────▼───┐                                             
                                │Internet│                                             
                                └────┬───┘                                             
    ┌──────────────────► ┌───────────▼───────────┐                                     
┌───┼──┐                 │Network Provider Decode│                                     
│Worker│                 └───────────┬───────────┘                                     
│Thread│                        ┌────▼──────┐                                          
└───┬──┘                        │Opus Decode│                                          
    └─────────────────────────► └────┬──────┘                                          
    ┌─────────────────► ┌────────────▼────────────┐                                    
    │                   │OnAudioFilterReadVcOutput│                                    
┌───┴──┐                └────────────┬────────────┘                                    
│Audio │             ┌───────────────▼─────────────────┐                               
│Thread│             │              NetEQ              │                               
└───┬──┘             │security-union/videocall-rs/neteq│                               
    │                └───────────────┬─────────────────┘                               
    └─────────────────────► ┌────────▼─────────┐                                       
                            │Unity Audio Source│                                       
                            └──────────────────┘                                       
</pre>
</div>
