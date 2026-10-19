cmake_minimum_required(VERSION 3.15)
project(OmniPdfCore CXX C)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

# Embedding VC++ runtime statically for Windows standalone portable builds
if(MSVC)
    set(CMAKE_MSVC_RUNTIME_LIBRARY "MultiThreaded$<$<CONFIG:Debug>:Debug>")
    add_compile_options(/W4 /EHsc /utf-8)
    add_definitions(-DOMNI_PDF_EXPORTS)
else()
    add_compile_options(-Wall -Wextra -fPIC -fvisibility=hidden)
endif()

include_directories(
    ${CMAKE_CURRENT_SOURCE_DIR}/include
)

set(CORE_SOURCES
    src/native_pdf_engine.cpp
    src/pdf_tile_rasterizer.cpp
    src/pdf_ast_editor.cpp
    src/pdf_security_crypto.cpp
    src/image_perspective_warp.cpp
    src/text_stream_extractor.cpp
)

set(CORE_HEADERS
    include/native_pdf_engine.h
    include/pdf_tile_rasterizer.h
    include/pdf_ast_editor.h
    include/pdf_security_crypto.h
    include/image_perspective_warp.h
    include/text_stream_extractor.h
)

# Build shared library (DLL on Windows, .so on Android/Linux)
add_library(pdfengine SHARED
    ${CORE_SOURCES}
    ${CORE_HEADERS}
)

target_include_directories(pdfengine PUBLIC
    $<BUILD_INTERFACE:${CMAKE_CURRENT_SOURCE_DIR}/include>
    $<INSTALL_INTERFACE:include>
)

if(WIN32)
    set_target_properties(pdfengine PROPERTIES
        OUTPUT_NAME "pdfengine"
        RUNTIME_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/bin"
    )
elseif(ANDROID)
    target_link_libraries(pdfengine log jnigraphics)
endif()
