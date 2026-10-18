"""OpenMic LAN discovery — dependency-free UDP broadcast.

So the desktop can find phones without anyone typing an IP. The phone broadcasts
a small JSON beacon a few times a second; the desktop listens and lists what it
hears. No external library (no zeroconf) — just UDP broadcast on the LAN.

Beacon payload (UTF-8 JSON on DISCOVERY_PORT):
    {"magic": "OPENMIC", "name": "Pixel 7", "port": 8126, "proto": 1,
     "auth": true}
`auth` is true when the phone requires a PIN. The beacon never contains the PIN.
"""

import json
import socket
import time

import openmic_proto as p


def make_beacon(name: str, port: int, auth: bool) -> bytes:
    return json.dumps({
        "magic": p.DISCOVERY_MAGIC, "name": name, "port": port,
        "proto": p.PROTO_VERSION, "auth": auth,
    }).encode("utf-8")


def broadcast_loop(name: str, port: int, auth: bool, stop, interval=1.0):
    """Broadcast a beacon until stop() returns True. Used by the phone side."""
    sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    sock.setsockopt(socket.SOL_SOCKET, socket.SO_BROADCAST, 1)
    beacon = make_beacon(name, port, auth)
    try:
        while not stop():
            sock.sendto(beacon, ("255.255.255.255", p.DISCOVERY_PORT))
            time.sleep(interval)
    finally:
        sock.close()


def discover(timeout=3.0):
    """Listen for beacons for `timeout` seconds. Return a list of unique phones:
    [{"name", "ip", "port", "auth"}]."""
    sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    sock.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
    sock.bind(("", p.DISCOVERY_PORT))
    sock.settimeout(0.5)
    found = {}
    end = time.time() + timeout
    while time.time() < end:
        try:
            data, addr = sock.recvfrom(1024)
        except socket.timeout:
            continue
        try:
            obj = json.loads(data.decode("utf-8"))
        except (ValueError, UnicodeDecodeError):
            continue
        if obj.get("magic") != p.DISCOVERY_MAGIC:
            continue
        ip = addr[0]
        found[ip] = {
            "name": obj.get("name", "phone"), "ip": ip,
            "port": int(obj.get("port", p.DEFAULT_PORT)),
            "auth": bool(obj.get("auth", False)),
        }
    sock.close()
    return list(found.values())


if __name__ == "__main__":
    print("Listening for OpenMic phones (3s)...")
    for ph in discover():
        lock = " [PIN required]" if ph["auth"] else ""
        print(f"  {ph['name']}  ->  {ph['ip']}:{ph['port']}{lock}")
