# Neolink 0.6.2 – HomeCam stream2

Modified by HomeCam Monitor, 2026-10-15. This is a separate executable based on
QuantumEntangledAndy/neolink commit e47a0d5734b0a06b53bd8d78be9ca2f6b8396480.
The original AGPL-3.0-or-later license applies to Neolink and these changes.
This is not an upstream Neolink release.

Changes:

- Exclude loopback, unspecified and multicast addresses from public UID lookups.
- Route binary channel-zero video with a nonmatching message number only when
  exactly one video consumer is open on that camera connection. Matching
  subscriptions retain priority. Other channels, XML/acknowledgements and
  ambiguous multiple-consumer cases retain the original routing. Regression
  tests cover these boundaries.
- Allow 15 seconds between stream watchdog feeds instead of three seconds.
- Update the locked `time` dependency to 0.3.37 for compatibility with Rust 1.85.1;
  Cargo also updates its required locked dependency versions.

The complete modified source is shipped in
`licenses/neolink-0.6.2-homecam-stream2-source.zip`, including the original license,
the patch, dependency lock and this notice. No camera configuration or credentials
are included.

Build normally with Rust 1.85.1, MSVC/Windows SDK and the GStreamer 1.26 development
libraries: `cargo build --release --locked -j 2`. The included
`build-neolink-stream.ps1` documents the Windows packaging build against the
existing GStreamer DLLs. `PkgConfig.cs` is a build-only metadata adapter; the Rust
FFI declarations and generated MSVC import libraries link to the existing runtime.
The original upstream source archive, this patch directory and the component lock
are required by that packaging script; a normal Cargo source build does not need it.

Test the routing change with
`cargo test --release --locked -j 2 -p neolink_core legacy_video`.


Additional stream2 change:

- Correct the UDP ACK feedback field to unique received payload bytes per second,
  replacing the inter-ACK arrival-time average. Update over real one-second-or-longer
  windows, do not count duplicate packets, and saturate the 32-bit protocol field.
  Three regression tests cover rate calculation, window reset, elapsed time and overflow.
- Protocol comparison reference: alzgamer-kr/pyneolink, core/udp_transport.py and
  docs/05-udp-p2p-transport.md (receive-rate ACK behavior). The implementation here
  is a separate Rust change, retaining Neolink's original license.
- No experimental raw-video capture or timestamp/live-pipeline changes are included.

Run the new unit tests with:
`cargo test --release --locked -j 2 -p neolink_core ack_receive_rate`
