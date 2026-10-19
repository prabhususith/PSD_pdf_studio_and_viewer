#include "win32_window.h"
#include <dwmapi.h>

namespace {
constexpr const wchar_t kWindowClassName[] = L"OMNI_PDF_STUDIO_WINDOW";
}

Win32Window::Win32Window() {}
Win32Window::~Win32Window() {
  Destroy();
}

bool Win32Window::Create(const std::wstring& title, const Point& origin, const Size& size) {
  Destroy();

  WNDCLASS window_class{};
  window_class.hCursor = LoadCursor(nullptr, IDC_ARROW);
  window_class.lpszClassName = kWindowClassName;
  window_class.style = CS_HREDRAW | CS_VREDRAW;
  window_class.cbClsExtra = 0;
  window_class.cbWndExtra = 0;
  window_class.hInstance = GetModuleHandle(nullptr);
  window_class.hIcon = LoadIcon(window_class.hInstance, MAKEINTRESOURCE(101));
  window_class.hbrBackground = (HBRUSH)(COLOR_WINDOW + 1);
  window_class.lpfnWndProc = WndProc;

  RegisterClass(&window_class);

  RECT window_rect = {
      static_cast<LONG>(origin.x),
      static_cast<LONG>(origin.y),
      static_cast<LONG>(origin.x + size.width),
      static_cast<LONG>(origin.y + size.height),
  };
  AdjustWindowRect(&window_rect, WS_OVERLAPPEDWINDOW, FALSE);

  window_handle_ = CreateWindow(
      kWindowClassName, title.c_str(),
      WS_OVERLAPPEDWINDOW,
      origin.x, origin.y,
      window_rect.right - window_rect.left,
      window_rect.bottom - window_rect.top,
      nullptr, nullptr, GetModuleHandle(nullptr), this);

  if (!window_handle_) {
    return false;
  }

  // Windows 11 Mica / Dark mode styling
  BOOL darkMode = TRUE;
  DwmSetWindowAttribute(window_handle_, 20 /* DWMWA_USE_IMMERSIVE_DARK_MODE */, &darkMode, sizeof(darkMode));

  int backdropType = 2; // DWMSBT_MAINWINDOW (Mica)
  DwmSetWindowAttribute(window_handle_, 38 /* DWMWA_SYSTEMBACKDROP_TYPE */, &backdropType, sizeof(backdropType));

  return OnCreate();
}

bool Win32Window::Show() {
  if (!window_handle_) return false;
  ShowWindow(window_handle_, SW_SHOWNORMAL);
  UpdateWindow(window_handle_);
  return true;
}

void Win32Window::Destroy() {
  if (window_handle_) {
    DestroyWindow(window_handle_);
    window_handle_ = nullptr;
  }
}

LRESULT CALLBACK Win32Window::WndProc(HWND const window, UINT const message, WPARAM const wparam, LPARAM const lparam) noexcept {
  if (message == WM_NCCREATE) {
    auto window_struct = reinterpret_cast<CREATESTRUCT*>(lparam);
    SetWindowLongPtr(window, GWLP_USERDATA, reinterpret_cast<LONG_PTR>(window_struct->lpCreateParams));
    auto that = static_cast<Win32Window*>(window_struct->lpCreateParams);
    that->window_handle_ = window;
  } else if (Win32Window* that = reinterpret_cast<Win32Window*>(GetWindowLongPtr(window, GWLP_USERDATA))) {
    return that->MessageHandler(window, message, wparam, lparam);
  }
  return DefWindowProc(window, message, wparam, lparam);
}

LRESULT Win32Window::MessageHandler(HWND window, UINT const message, WPARAM const wparam, LPARAM const lparam) noexcept {
  switch (message) {
    case WM_DESTROY:
      window_handle_ = nullptr;
      OnDestroy();
      if (quit_on_close_) {
        PostQuitMessage(0);
      }
      return 0;
  }
  return DefWindowProc(window, message, wparam, lparam);
}
