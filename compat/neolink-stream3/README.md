# Neolink 0.6.2 – HomeCam stream3

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
`licenses/neolink-0.6.2-homecam-stream3-source.zip`, including the original license,
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


Additional stream3 change:

- Correct the UDP ACK feedback field to unique received payload bytes per second,
  replacing the inter-ACK arrival-time average. Update over real one-second-or-longer
  windows, do not count duplicate packets, and saturate the 32-bit protocol field.
  Three regression tests cover rate calculation, window reset, elapsed time and overflow.
- Protocol comparison reference: alzgamer-kr/pyneolink, core/udp_transport.py and
  docs/05-udp-p2p-transport.md (receive-rate ACK behavior). The implementation here
  is a separate Rust change, retaining Neolink's original license.
- Retains all stream2 routing and ACK-rate fixes. Live-pipeline changes are listed below.

Run the new unit tests with:
`cargo test --release --locked -j 2 -p neolink_core ack_receive_rate`

Live pipeline changes in stream3:

- Configure H264/H265 and audio appsrc as live, non-seekable time-formatted streams.
- Enforce blocking backpressure, 2 MiB and 500 ms appsrc limits instead of the previous nonblocking 50 MiB advisory limit; intermediate channel holds eight rather than 100 buffers.
- Disable queue2 download-style buffering and reduce its time limit from five seconds to 250 ms. Compressed packets are not arbitrarily leaked from this queue.
- Replay only the latest GOP starting at its I-frame; without an I-frame, wait for the next one. Preserve initial video timestamps. After a source broadcast overrun, wait for an I-frame before forwarding dependent frames again.
- Production-pipeline integration test injects a synthetic H264 I-frame at a 50 ms cadence, counts complete RTP marker frames and checks timestamp lag/queued time. Run with the private GStreamer runtime on PATH and HOMECAM_LIVE_SAMPLE pointing to the synthetic H264 file:
  cargo test --release --locked --bin neolink homecam_live_tests -- --include-ignored --nocapture
- No real camera configuration, credentials, network routing or UID login protocol changes are included. End-to-end camera/relay latency still needs user comparison; the local test does not prove a 15-second remote lag is eliminated.

References: GStreamer appsrc and queue2 documentation at https://gstreamer.freedesktop.org/documentation/app/appsrc.html and https://gstreamer.freedesktop.org/documentation/coreelements/queue2.html .
