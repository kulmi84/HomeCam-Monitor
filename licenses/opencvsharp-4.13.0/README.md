# OpenCvSharp runtime provenance

- NuGet OpenCvSharp4 / OpenCvSharp4.runtime.win 4.13.0.20260627.
- Wrapper source: https://github.com/shimat/opencvsharp/tree/b161e7e012f5101f6d5dc68a835c59db6cc88b18
- OpenCV 4.13.0 (runtime build fe38fc6): https://github.com/opencv/opencv/tree/4.13.0
- Contrib 4.13.0 (runtime build d99ad2a): https://github.com/opencv/opencv_contrib/tree/4.13.0
- Intel IPP/IW 2022.2.0: https://github.com/opencv/opencv_3rdparty/tree/c934a2a15a6df020446ac3dfa07e3acf72b63a8f/ippicv
- JPEG-turbo (license reference 3.1.3; the binary reports JPEG ABI 70), libpng 1.6.55, libwebp 1.6.0, libtiff 4.7.1, zlib 1.3.1: license files from official tagged sources. OpenJPEG, OpenEXR, Protobuf, Flatbuffers and ITT notices are retained from OpenCV's source archive.
- License/NOTICE/COPYING/COPYRIGHT files retain their source paths. IPP EULA and third-party-programs files are included separately.
- Build information is obtained from the actual distributed native library.
- Unused opencv_videoio_ffmpeg plugin is excluded. Motion decoding uses the already documented HomeCam FFmpeg build.
