12/20/2025 (v4.2):
- MirrorNetProvider: Fix null reference if a player joins late with incoming audio frames #21. Thanks @EterniumDev

10/27/2025 (v4.1):
- Fix VcAudioSourceOutput non-deterministic initialization order bug that caused NREs. Thanks @ctxpower on Discord!

10/15/2025 (v4):
- Netcode for GameObjects support by @ERisberg. Thank you so much!
- Netick support by @Milk-Drinker01. Thank you so much!
- [MulticastVcAudioOutput](Output/Multicast/MulticastVcAudioOutput.cs), an audio output that copies its audio to other audio outputs
- README updates

8/21/2025 (v3):
- FishNet support by @maxkratt. Thank you so much!
- Change Mirror implementation define symbol #if
- Audio source play on awake set to false and stopped during config
- README updates

8/7/2025 (v2.3):
- Changes by @TheTechWiz5305
    - Mirror network provider now includes #if MIRROR || UNITY_SERVER to make server build usage of MetaVoiceChat easier
    - .gitignore and delete LICENSE.meta (GitHub thinks it is another license)

7/27/2025 (v2.2):
- Video tutorial: https://youtu.be/2fSqSAnRS5M
- Discord support and contact section in README
- Echo empty frames locally even when echo is disabled
- Add MirrorVR to README
- Namespace RnnoiseVcInputFilter

7/18/2025 (v2.1):
- Optional [rnnoise](https://github.com/xiph/rnnoise) with [Vatsal Ambastha's RNNoise4Unity](https://github.com/adrenak/RNNoise4Unity) and [RnnoiseVcInputFilter](rnnoise/RnnoiseVcInputFilter.cs)
- Renamed first and next input and output filters for more clarity
- More documentation
- More example stuff

7/17/2025 (v2):
- Example code and screenshots for advanced usage
- 48kHz audio (was 16kHz)
- Improved VcMicAudioInput and VcMic
    - OnActiveDeviceChanged event
    - Automatic microphone reconnection
    - SetSelectedDevice(string device) to control automatic reconnection
- Better namespaces (removed Assets.Metater)
- Microphone devices listener utility
- Warnings for incorrect VC input and output filter usage
- Fixed VcAudioSourceOutput bug (audio loop when no data received instead of clearing)
- Added Mirror server build warning
- More intuitive local echo functionality
- Better documentation
