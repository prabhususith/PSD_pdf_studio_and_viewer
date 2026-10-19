#include <windows.h>
#include "flutter_window.h"
#include "../../core/include/native_pdf_engine.h"

int APIENTRY wWinMain(_In_ HINSTANCE instance, _In_opt_ HINSTANCE prev,
                      _In_ wchar_t *command_line, _In_ int show_command) {
  // Initialize native C++ PDF engine
  OmniPdf_Initialize();

  // Attach console in debug mode or if command line flag requested
  if (AttachConsole(ATTACH_PARENT_PROCESS)) {
    FILE *fp;
    freopen_s(&fp, "CONOUT$", "w", stdout);
    freopen_s(&fp, "CONOUT$", "w", stderr);
  }

  FlutterWindow window;
  Win32Window::Point origin(100, 100);
  Win32Window::Size size(1280, 800);
  if (!window.Create(L"Omni PDF Studio", origin, size)) {
    return EXIT_FAILURE;
  }
  window.SetQuitOnClose(true);
  window.Show();

  MSG msg;
  while (GetMessage(&msg, nullptr, 0, 0)) {
    TranslateMessage(&msg);
    DispatchMessage(&msg);
  }

  OmniPdf_Shutdown();
  return static_cast<int>(msg.wParam);
}
