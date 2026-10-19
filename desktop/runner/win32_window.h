#ifndef WIN32_WINDOW_H_
#define WIN32_WINDOW_H_

#include <windows.h>
#include <string>
#include <memory>

class Win32Window {
 public:
  struct Point {
    unsigned int x;
    unsigned int y;
    Point(unsigned int x, unsigned int y) : x(x), y(y) {}
  };

  struct Size {
    unsigned int width;
    unsigned int height;
    Size(unsigned int width, unsigned int height) : width(width), height(height) {}
  };

  Win32Window();
  virtual ~Win32Window();

  bool Create(const std::wstring& title, const Point& origin, const Size& size);
  bool Show();
  void Destroy();
  HWND GetHandle() const { return window_handle_; }
  void SetQuitOnClose(bool quit_on_close) { quit_on_close_ = quit_on_close; }

 protected:
  virtual LRESULT MessageHandler(HWND window, UINT const message, WPARAM const wparam, LPARAM const lparam) noexcept;
  virtual bool OnCreate() { return true; }
  virtual void OnDestroy() {}

 private:
  static LRESULT CALLBACK WndProc(HWND const window, UINT const message, WPARAM const wparam, LPARAM const lparam) noexcept;

  bool quit_on_close_ = false;
  HWND window_handle_ = nullptr;
};

#endif  // WIN32_WINDOW_H_
