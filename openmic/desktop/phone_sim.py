#!/usr/bin/env python3
"""OpenMic phone simulator.

Stands in for the Android app so you can test the desktop receiver without a
phone. It runs the OpenMic server (like the phone does), accepts one desktop
connection, and streams a generated sine tone as PCM16 audio frames.

    python3 phone_sim.py            # listen on 0.0.0.0:8126, 440 Hz tone
    python3 openmic_server.py 127.0.0.1 --sink wav --out test.wav

Then test.wav should contain the tone.
"""

import argparse
import json
import math
import socket
import struct
import time

import openmic_proto as p


def gen_frame(sample_rate, samples, phase, freq):
    """Generate `samples` of a sine tone, return (pcm16_bytes, new_phase)."""
    out = bytearray()
    step = 2 * math.pi * freq / sample_rate
    amp = 12000
    for _ in range(samples):
        out += struct.pack("<h", int(amp * math.sin(phase)))
        phase += step
    return bytes(out), phase


def serve(args):
    srv = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    srv.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
    srv.bind((args.bind, args.port))
    srv.listen(1)
    print(f"[phone_sim] listening on {args.bind}:{args.port} "
          f"({args.rate} Hz tone). Connect the desktop receiver.")

    conn, addr = srv.accept()
    print(f"[phone_sim] desktop connected from {addr}")

    frame_samples = int(args.rate * args.frame_ms / 1000)
    conn.sendall(p.pack_json(p.T_HELLO, {
        "proto": p.PROTO_VERSION, "sampleRate": args.rate, "channels": 1,
        "codec": p.CODEC_PCM16, "frameSamples": frame_samples,
        "device": "PhoneSimulator", "appVersion": "sim-1.0"}))

    msg_type, payload = p.read_frame(conn)
    if msg_type != p.T_START:
        print(f"[phone_sim] expected START, got {msg_type:#x}; closing")
        conn.close(); return
    print(f"[phone_sim] desktop accepted: {json.loads(payload)}")

    seq = 0
    phase = 0.0
    t0 = time.time()
    try:
        while time.time() - t0 < args.seconds:
            pcm, phase = gen_frame(args.rate, frame_samples, phase, args.freq)
            ts_ms = int((time.time() - t0) * 1000)
            conn.sendall(p.pack_audio(seq, ts_ms, pcm))
            seq += 1
            time.sleep(args.frame_ms / 1000)
        conn.sendall(p.pack_frame(p.T_STOP))
        print(f"\n[phone_sim] sent {seq} frames, done.")
    except (BrokenPipeError, ConnectionError):
        print(f"\n[phone_sim] desktop disconnected after {seq} frames.")
    finally:
        conn.close(); srv.close()


def main():
    ap = argparse.ArgumentParser(description="OpenMic phone simulator")
    ap.add_argument("--bind", default="0.0.0.0")
    ap.add_argument("-p", "--port", type=int, default=p.DEFAULT_PORT)
    ap.add_argument("--rate", type=int, default=16000)
    ap.add_argument("--frame-ms", type=int, default=20)
    ap.add_argument("--freq", type=float, default=440.0, help="tone frequency")
    ap.add_argument("--seconds", type=float, default=3.0)
    serve(ap.parse_args())


if __name__ == "__main__":
    main()
