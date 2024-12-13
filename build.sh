#!/bin/bash -eu

export LIBWEBRTC_DOWNLOAD_URL=https://github.com/Unity-Technologies/com.unity.webrtc/releases/download/M116/webrtc-linux.zip
export SOLUTION_DIR=$(pwd)/Plugin~
export OUTPUT_FILEPATH=$(pwd)/Runtime/Plugins/x86_64/libwebrtc.so
export LIBCXX_BUILD_DIR=$(pwd)/llvm-project/build

source ~/.profile

# Download LibWebRTC 
unzip -d $SOLUTION_DIR/webrtc ./BuildScripts~/webrtc-linux 

# Build UnityRenderStreaming Plugin 
pushd "$SOLUTION_DIR"
cmake --preset=x86_64-linux -DCMAKE_VERBOSE_MAKEFILE=1
cmake --build --preset=release-linux --target WebRTCPlugin
popd

strip --strip-unneeded "$OUTPUT_FILEPATH"
