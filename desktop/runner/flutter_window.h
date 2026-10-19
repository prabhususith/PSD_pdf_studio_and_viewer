#ifndef FLUTTER_WINDOW_H_
#define FLUTTER_WINDOW_H_

#include "win32_window.h"
#include <memory>

class FlutterWindow : public Win32Window {
 public:
  FlutterWindow();
  virtual ~FlutterWindow();

 protected:
  bool OnCreate() override;
  void OnDestroy() override;
  LRESULT MessageHandler(HWND window, UINT const message, WPARAM const wparam, LPARAM const lparam) noexcept override;
};

#endif  // FLUTTER_WINDOW_H_
