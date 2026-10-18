#!/usr/bin/env python3
"""OpenMic desktop receiver.

Connects to the phone (which runs the OpenMic server), negotiates audio
parameters, then receives encoded audio frames and sends the decoded PCM to an
output "sink".

Sinks (choose with --sink):
  * speaker  : play through the default output device (needs `sounddevice`)
  * wav      : write everything to a .wav file (great for testing, no audio HW)
  * virtual  : play into a virtual audio device by name, so other apps see it
               as a microphone. On Windows install VB-CABLE (free) and pass
               --device "CABLE Input"; on Linux create a null sink + source
               with PulseAudio/PipeWire. See README.md.

This file is dependency-free for the `wav` sink. `speaker`/`virtual` need:
    pip install sounddevice numpy
"""

import argparse
import socket
import sys
import time
import wave

import openmic_proto as p


def make_sink(args, sample_rate, channels):
    """Return a callable sink(pcm_bytes) and a close() callable."""
    if args.sink == "wav":
        wf = wave.open(args.out, "wb")
        wf.setnchannels(channels)
        wf.setsampwidth(2)  # 16-bit
        wf.setframerate(sample_rate)
        return (lambda b: wf.writeframes(b)), wf.close

    # speaker / virtual both use sounddevice
    try:
        import numpy as np
        import sounddevice as sd
    except ImportError:
        sys.exit("The '%s' sink needs: pip install sounddevice numpy" % args.sink)

    device = args.device if args.sink == "virtual" else None
    stream = sd.RawOutputStream(samplerate=sample_rate, channels=channels,
                                dtype="int16", device=device)
    stream.start()
    return (lambda b: stream.write(b)), (lambda: (stream.stop(), stream.close()))


def make_decoder(codec, sample_rate, channels):
    """Return a decode(encoded_bytes) -> pcm16_bytes callable."""
    if codec == p.CODEC_PCM16:
        return lambda enc: enc
    if codec == p.CODEC_OPUS:
        try:
            from opuslib import Decoder  # pip install opuslib (+ libopus)
        except ImportError:
            sys.exit("Opus selected but 'opuslib' is not installed.")
        dec = Decoder(sample_rate, channels)
        # frame size in samples/channel; 20 ms by default
        frame = int(sample_rate * 0.02)
        return lambda enc: dec.decode(enc, frame)
    sys.exit("Unknown codec id %d" % codec)


def run(args):
    print(f"[openmic] connecting to phone {args.host}:{args.port} ...")
    sock = socket.create_connection((args.host, args.port), timeout=10)
    sock.settimeout(None)

    msg_type, payload = p.read_frame(sock)
    if msg_type != p.T_HELLO:
        sys.exit(f"expected HELLO, got {msg_type:#x}")
    import json
    hello = json.loads(payload.decode("utf-8"))
    print(f"[openmic] phone says: {hello}")

    sample_rate = args.rate or hello.get("sampleRate", 16000)
    channels = hello.get("channels", 1)
    codec = hello.get("codec", p.CODEC_PCM16)

    sock.sendall(p.pack_json(p.T_START, {
        "sampleRate": sample_rate, "channels": channels, "codec": codec}))

    sink, close_sink = make_sink(args, sample_rate, channels)
    decode = make_decoder(codec, sample_rate, channels)
    print(f"[openmic] streaming: {sample_rate} Hz, {channels} ch, "
          f"codec={codec}, sink={args.sink}. Ctrl-C to stop.")

    frames = 0
    last_seq = -1
    lost = 0
    t0 = time.time()
    try:
        while True:
            msg_type, payload = p.read_frame(sock)
            if msg_type == p.T_AUDIO:
                seq, ts_ms, enc = p.unpack_audio(payload)
                if last_seq >= 0 and seq != last_seq + 1:
                    lost += (seq - last_seq - 1)
                last_seq = seq
                sink(decode(enc))
                frames += 1
                if frames % 50 == 0:
                    secs = time.time() - t0
                    print(f"\r[openmic] {frames} frames, {lost} lost, "
                          f"{secs:5.1f}s", end="", flush=True)
            elif msg_type == p.T_PING:
                sock.sendall(p.pack_frame(p.T_PONG))
            elif msg_type == p.T_STOP:
                print("\n[openmic] phone sent STOP")
                break
    except (KeyboardInterrupt, ConnectionError) as e:
        print(f"\n[openmic] stopping ({type(e).__name__})")
    finally:
        try:
            sock.sendall(p.pack_frame(p.T_STOP))
        except OSError:
            pass
        sock.close()
        close_sink()
        print(f"[openmic] done: {frames} frames received, {lost} lost.")


def main():
    ap = argparse.ArgumentParser(description="OpenMic desktop receiver")
    ap.add_argument("host", help="phone IP address shown in the OpenMic app")
    ap.add_argument("-p", "--port", type=int, default=p.DEFAULT_PORT)
    ap.add_argument("--sink", choices=["speaker", "wav", "virtual"],
                    default="speaker")
    ap.add_argument("--out", default="openmic_capture.wav",
                    help="output file for --sink wav")
    ap.add_argument("--device", default=None,
                    help="output device name for --sink virtual "
                         '(e.g. "CABLE Input")')
    ap.add_argument("--rate", type=int, default=None,
                    help="override sample rate")
    run(ap.parse_args())


if __name__ == "__main__":
    main()
