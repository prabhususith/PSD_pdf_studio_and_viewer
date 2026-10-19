#include "flutter_window.h"
#include <windows.h>

FlutterWindow::FlutterWindow() {}
FlutterWindow::~FlutterWindow() {}

bool FlutterWindow::OnCreate() {
  return true;
}

void FlutterWindow::OnDestroy() {}

LRESULT FlutterWindow::MessageHandler(HWND hwnd, UINT const message, WPARAM const wparam, LPARAM const lparam) noexcept {
  return Win32Window::MessageHandler(hwnd, message, wparam, lparam);
}
