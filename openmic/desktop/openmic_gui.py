#!/usr/bin/env python3
"""OpenMic desktop app — a small window, no command line.

Scan for your phone on the LAN (or type its IP), pick where the audio should go,
and press Start. Uses the same receive core as the command-line tool
(openmic_client.Receiver), so behaviour is identical.

Needs Python with Tk (standard on Windows/macOS; on Linux: `apt install
python3-tk`). Live playback needs `pip install sounddevice numpy`; the "WAV file"
output needs nothing.
"""

import json
import os
import queue
import tkinter as tk
from tkinter import filedialog, ttk

import openmic_discovery
import openmic_fx
import openmic_proto as p
from openmic_client import Receiver, list_output_devices

CONFIG = os.path.join(os.path.expanduser("~"), ".openmic_gui.json")


def load_config():
    try:
        with open(CONFIG) as f:
            return json.load(f)
    except Exception:  # noqa: BLE001
        return {}


def save_config(cfg):
    try:
        with open(CONFIG, "w") as f:
            json.dump(cfg, f)
    except Exception:  # noqa: BLE001
        pass


class App:
    def __init__(self, root):
        self.root = root
        self.cfg = load_config()
        self.rx = None
        self.events = queue.Queue()      # worker-thread -> UI messages
        self.phones = []                 # discovered {name, ip, port, auth}

        root.title("OpenMic")
        root.minsize(420, 440)
        pad = dict(padx=10, pady=4)
        frm = ttk.Frame(root, padding=12)
        frm.pack(fill="both", expand=True)

        ttk.Label(frm, text="OpenMic", font=("", 18, "bold")).pack(anchor="w")
        ttk.Label(frm, text="Turn your phone into a PC microphone.").pack(anchor="w")

        # --- phone selection ---
        box = ttk.LabelFrame(frm, text="1. Phone", padding=8)
        box.pack(fill="x", pady=(10, 4))
        self.listbox = tk.Listbox(box, height=4)
        self.listbox.pack(fill="x")
        row = ttk.Frame(box); row.pack(fill="x", pady=(6, 0))
        self.scan_btn = ttk.Button(row, text="Scan Wi-Fi", command=self.scan)
        self.scan_btn.pack(side="left")
        ttk.Label(row, text="  or IP:").pack(side="left")
        self.ip_var = tk.StringVar(value=self.cfg.get("ip", ""))
        ttk.Entry(row, textvariable=self.ip_var, width=16).pack(side="left")
        ttk.Label(row, text="PIN:").pack(side="left", padx=(8, 0))
        self.pin_var = tk.StringVar(value=self.cfg.get("pin", ""))
        ttk.Entry(row, textvariable=self.pin_var, width=8).pack(side="left")

        # --- output selection ---
        box2 = ttk.LabelFrame(frm, text="2. Send audio to", padding=8)
        box2.pack(fill="x", pady=4)
        self.sink_var = tk.StringVar(value=self.cfg.get("sink", "speaker"))
        ttk.Radiobutton(box2, text="Speakers (hear it now)", value="speaker",
                        variable=self.sink_var, command=self._sync).pack(anchor="w")
        rowv = ttk.Frame(box2); rowv.pack(fill="x", anchor="w")
        ttk.Radiobutton(rowv, text="Virtual mic device:", value="virtual",
                        variable=self.sink_var, command=self._sync).pack(side="left")
        self.device_var = tk.StringVar(value=self.cfg.get("device", ""))
        self.device_combo = ttk.Combobox(rowv, textvariable=self.device_var,
                                          width=24, values=list_output_devices())
        self.device_combo.pack(side="left", padx=4)
        roww = ttk.Frame(box2); roww.pack(fill="x", anchor="w")
        ttk.Radiobutton(roww, text="WAV file:", value="wav",
                        variable=self.sink_var, command=self._sync).pack(side="left")
        self.wav_var = tk.StringVar(value=self.cfg.get("wav", "openmic_capture.wav"))
        ttk.Entry(roww, textvariable=self.wav_var, width=22).pack(side="left", padx=4)
        ttk.Button(roww, text="...", width=3, command=self.pick_wav).pack(side="left")
        ttk.Label(box2, text="Tip: for apps like Discord, install VB-CABLE and "
                  'pick "CABLE Input" here.', foreground="#666",
                  wraplength=380).pack(anchor="w", pady=(4, 0))

        # --- voice changer ---
        box3 = ttk.LabelFrame(frm, text="3. Voice changer", padding=8)
        box3.pack(fill="x", pady=4)
        self.effect_var = tk.StringVar(value=self.cfg.get("effect", "None"))
        self.effect_combo = ttk.Combobox(box3, textvariable=self.effect_var,
                                          values=openmic_fx.PRESETS, width=16,
                                          state="readonly")
        self.effect_combo.pack(side="left")
        self.effect_combo.bind("<<ComboboxSelected>>", self.on_effect_change)
        note = ("numpy needed: pip install numpy" if not openmic_fx.HAVE_NUMPY
                else "Change it live while streaming.")
        ttk.Label(box3, text="  " + note, foreground="#666").pack(side="left")

        # --- controls ---
        ctl = ttk.Frame(frm); ctl.pack(fill="x", pady=8)
        self.start_btn = ttk.Button(ctl, text="Start", command=self.start)
        self.start_btn.pack(side="left")
        self.stop_btn = ttk.Button(ctl, text="Stop", command=self.stop,
                                   state="disabled")
        self.stop_btn.pack(side="left", padx=6)

        self.status = tk.StringVar(value="Idle. Scan for your phone, or type its IP.")
        ttk.Label(frm, textvariable=self.status, wraplength=390,
                  foreground="#0a60c0").pack(anchor="w", pady=(6, 0))

        if not list_output_devices():
            self.status.set("Idle. (Live playback needs: pip install sounddevice "
                            "numpy — or use WAV file output.)")

        self._sync()
        root.after(100, self._drain_events)
        root.protocol("WM_DELETE_WINDOW", self.on_close)

    # ---- phone discovery (runs discover() off the UI thread) ----
    def scan(self):
        self.scan_btn.config(state="disabled", text="Scanning...")
        self.status.set("Searching the Wi-Fi for phones (3s)...")
        import threading
        threading.Thread(target=self._scan_worker, daemon=True).start()

    def _scan_worker(self):
        phones = openmic_discovery.discover()
        self.events.put(("phones", phones))

    def _show_phones(self, phones):
        self.phones = phones
        self.listbox.delete(0, "end")
        for ph in phones:
            lock = "  [PIN]" if ph["auth"] else ""
            self.listbox.insert("end", f"{ph['name']}   {ph['ip']}:{ph['port']}{lock}")
        self.scan_btn.config(state="normal", text="Scan Wi-Fi")
        if phones:
            self.listbox.selection_set(0)
            self.status.set(f"Found {len(phones)} phone(s). Pick one and press Start.")
        else:
            self.status.set("No phones found. Open OpenMic on the phone and press "
                            "Start there, then Scan again — or type the IP.")

    def _sync(self):
        kind = self.sink_var.get()
        self.device_combo.config(state="readonly" if kind == "virtual" else "disabled")

    def pick_wav(self):
        path = filedialog.asksaveasfilename(defaultextension=".wav",
                                            filetypes=[("WAV", "*.wav")])
        if path:
            self.wav_var.set(path)
            self.sink_var.set("wav"); self._sync()

    # ---- start / stop ----
    def _selected_phone(self):
        ip = self.ip_var.get().strip()
        if ip:
            return ip, p.DEFAULT_PORT
        sel = self.listbox.curselection()
        if sel and self.phones:
            ph = self.phones[sel[0]]
            return ph["ip"], ph["port"]
        return None, None

    def start(self):
        host, port = self._selected_phone()
        if not host:
            self.status.set("Pick a scanned phone or type its IP first.")
            return
        self._save()
        self.rx = Receiver(
            host, port, pin=self.pin_var.get().strip() or None,
            sink=self.sink_var.get(), device=self.device_var.get() or None,
            out_path=self.wav_var.get(), effect=self.effect_var.get(),
            on_status=lambda s: self.events.put(("status", s)),
            on_stats=lambda f, l, s: self.events.put(("stats", (f, l, s))),
            on_stopped=lambda r, e: self.events.put(("stopped", (r, e))))
        self.rx.start()
        self.start_btn.config(state="disabled")
        self.stop_btn.config(state="normal")

    def stop(self):
        if self.rx:
            self.rx.stop()

    def on_effect_change(self, _event=None):
        """Apply the voice-changer preset live if a session is running."""
        if self.rx:
            self.rx.set_effect(self.effect_var.get())
        self._save()

    # ---- marshal worker events onto the Tk thread ----
    def _drain_events(self):
        try:
            while True:
                kind, data = self.events.get_nowait()
                if kind == "phones":
                    self._show_phones(data)
                elif kind == "status":
                    self.status.set(data)
                elif kind == "stats":
                    f, l, s = data
                    self.status.set(f"Streaming — {f} frames, {l} lost, {s:.0f}s")
                elif kind == "stopped":
                    reason, error = data
                    self.status.set(f"Error: {error}" if error else "Stopped.")
                    self.start_btn.config(state="normal")
                    self.stop_btn.config(state="disabled")
                    self.rx = None
        except queue.Empty:
            pass
        self.root.after(100, self._drain_events)

    def _save(self):
        save_config({
            "ip": self.ip_var.get().strip(), "pin": self.pin_var.get().strip(),
            "sink": self.sink_var.get(), "device": self.device_var.get(),
            "wav": self.wav_var.get(), "effect": self.effect_var.get()})

    def on_close(self):
        if self.rx:
            self.rx.stop()
        self._save()
        self.root.destroy()


def main():
    root = tk.Tk()
    App(root)
    root.mainloop()


if __name__ == "__main__":
    main()
