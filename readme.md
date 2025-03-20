# Transcription and Diarization

Wraps a few open source models to transcribe, diarize and optionally tag a specific speaker.

Briefly,
- Transcribe: Extract the audio from video (.mp4) files and convert the audio to text (speed-to-text).

- Diarize: Adds speaker tags to the transcript for multiple speakers. The speaker tags are generic markers, e.g. SPEAKER_01, SPEAKER_02, etc.

- Speaker Identification: Replaces the generic speaker tag with a configured speaker name using available audio sample embeddings of the speaker.

# Models
- [Fast Transcriber](https://github.com/Purfview/whisper-standalone-win) - transcription and diarization
- [Pyannote](https://huggingface.co/deepghs/pyannote-embedding-onnx) -  audio embedding model for speaker identification

# Solution Projects
### TranscriptionServiceHost
An F# windows service that exposes a SignalR connection to process transcription requests. The service queues incoming 'jobs' and processes them serially. The client is notified when the job is done.

Transcription, diarizaton and speaker identification are compute intensive and so the service is meant to run on a GPU-enabled machine. The intent is to increase the utilization of the GPU infrastructure by making it more easily shareable.

### TranscriptionClient 
An F# GUI application to submit jobs to the service. The client uploads the .mp4 files to the service and triggers the processing. When server processing is complete, the client downloads the transcipt (.vtt) files.

### TranscriptionService
Contains core logic for transcripton and diarization.

### TranscriptionInterop
Common definitions shared between client and server

### TranscriptionAndDiarization
Older project that contains batch scripts that were used to develop and refine the transcription processing.
