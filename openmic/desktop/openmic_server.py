#!/usr/bin/env python3
"""OpenMic desktop receiver (command line).

Connects to the phone, negotiates audio parameters, receives the stream and
sends it to an output sink. The actual pipeline lives in openmic_client.py so
the GUI (openmic_gui.py) shares the same code.

Sinks (choose with --sink):
  * speaker : play through the default output device (needs `sounddevice`)
  * wav     : write everything to a .wav file (great for testing, no audio HW)
  * virtual : play into a named device (e.g. "CABLE Input") so other apps see
              it as a microphone. See README.md.
"""

import argparse
import sys
import threading
import time

import openmic_proto as p
from openmic_client import Receiver


def resolve_host(args):
    """Return (host, port). USB tunnels to localhost; --discover finds the LAN IP."""
    if args.usb:
        import openmic_usb
        err = openmic_usb.forward(args.port)
        if err:
            sys.exit(err)
        print("[openmic] USB tunnel ready (adb forward).")
        return "127.0.0.1", args.port
    if not args.discover:
        if not args.host:
            sys.exit("Give the phone's IP, use --discover, or --usb.")
        return args.host, args.port
    import openmic_discovery
    print("[openmic] searching the LAN for phones (3s)...")
    phones = openmic_discovery.discover()
    if not phones:
        sys.exit("No OpenMic phones found. Is the app open and streaming on the "
                 "same Wi-Fi? You can also pass the IP directly.")
    if len(phones) == 1:
        ph = phones[0]
    else:
        for i, ph in enumerate(phones):
            print(f"  [{i}] {ph['name']}  {ph['ip']}:{ph['port']}"
                  f"{'  [PIN]' if ph['auth'] else ''}")
        ph = phones[int(input("Pick a phone number: "))]
    print(f"[openmic] found {ph['name']} at {ph['ip']}:{ph['port']}")
    return ph["ip"], ph["port"]


def run(args):
    host, port = resolve_host(args)
    done = threading.Event()

    def on_status(s):
        print(f"[openmic] {s}")

    def on_stats(frames, lost, secs):
        print(f"\r[openmic] {frames} frames, {lost} lost, {secs:5.1f}s",
              end="", flush=True)

    def on_stopped(reason, error):
        if error:
            print(f"\n[openmic] error: {error}")
        else:
            print("\n[openmic] stopped.")
        done.set()

    rx = Receiver(host, port, pin=args.pin, sink=args.sink, device=args.device,
                  out_path=args.out, rate=args.rate, effect=args.effect,
                  volume=args.volume, noise=args.noise,
                  on_status=on_status, on_stats=on_stats, on_stopped=on_stopped)
    rx.start()
    try:
        while not done.is_set():
            time.sleep(0.2)
    except KeyboardInterrupt:
        print("\n[openmic] stopping...")
        rx.stop()
        done.wait(3)


def main():
    ap = argparse.ArgumentParser(description="OpenMic desktop receiver")
    ap.add_argument("host", nargs="?", default=None,
                    help="phone IP shown in the app (omit if using --discover)")
    ap.add_argument("-p", "--port", type=int, default=p.DEFAULT_PORT)
    ap.add_argument("--discover", action="store_true",
                    help="find the phone on the LAN instead of typing its IP")
    ap.add_argument("--usb", action="store_true",
                    help="USB mode: adb forward + connect to localhost (low latency)")
    ap.add_argument("--pin", default=None, help="PIN if the phone requires one")
    ap.add_argument("--volume", type=float, default=1.0,
                    help="output volume multiplier (1.0 = unchanged)")
    ap.add_argument("--noise", type=float, default=0.0,
                    help="noise suppression amount, 0..1")
    ap.add_argument("--sink", choices=["speaker", "wav", "virtual"],
                    default="speaker")
    ap.add_argument("--out", default="openmic_capture.wav",
                    help="output file for --sink wav")
    ap.add_argument("--device", default=None,
                    help='output device name for --sink virtual (e.g. "CABLE Input")')
    ap.add_argument("--rate", type=int, default=None, help="override sample rate")
    ap.add_argument("--effect", default=None,
                    help="voice-changer preset: Chipmunk, Deep voice, Robot, "
                         "Alien, Echo, Megaphone, Monster")
    run(ap.parse_args())


if __name__ == "__main__":
    main()
