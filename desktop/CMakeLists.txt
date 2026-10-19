cmake_minimum_required(VERSION 3.15)
project(omni_pdf_desktop LANGUAGES CXX)

set(BINARY_NAME "omni_pdf")

# Statically link VC++ Runtime to guarantee zero external runtime dependencies on target machines
set(CMAKE_MSVC_RUNTIME_LIBRARY "MultiThreaded$<$<CONFIG:Debug>:Debug>")

cmake_policy(SET CMP0079 NEW)

add_subdirectory("../core" "${CMAKE_BINARY_DIR}/core")

add_executable(${BINARY_NAME} WIN32
  "runner/main.cpp"
  "runner/flutter_window.cpp"
  "runner/win32_window.cpp"
  "runner/Runner.rc"
)

target_include_directories(${BINARY_NAME} PRIVATE
  "${CMAKE_CURRENT_SOURCE_DIR}/runner"
  "${CMAKE_CURRENT_SOURCE_DIR}/../core/include"
)

target_compile_definitions(${BINARY_NAME} PRIVATE
  "NOMINMAX"
  "UNICODE"
  "_UNICODE"
)

target_compile_options(${BINARY_NAME} PRIVATE
  /W4 /EHsc /utf-8
)

target_link_libraries(${BINARY_NAME} PRIVATE
  pdfengine
  dwmapi
  shlwapi
)
